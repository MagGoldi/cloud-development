#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT_DIR="$(dirname "$SCRIPT_DIR")"

export PATH="$HOME/.yandex-cloud/bin:$PATH"

source "$SCRIPT_DIR/env.sh"

BUILD_DIR="$SCRIPT_DIR/build"
mkdir -p "$BUILD_DIR"

log()  { echo -e "\033[1;34m[INFO]\033[0m  $*"; }
ok()   { echo -e "\033[1;32m[OK]\033[0m    $*"; }
err()  { echo -e "\033[1;31m[ERROR]\033[0m $*" >&2; exit 1; }

command -v yc    >/dev/null 2>&1 || err "yc CLI не установлен. Запустите: curl -sSL https://storage.yandexcloud.net/yandexcloud-yc/install.sh | bash"
command -v dotnet >/dev/null 2>&1 || err "dotnet SDK не найден"
command -v zip    >/dev/null 2>&1 || err "zip не найден"

log "Используем каталог: $YC_FOLDER_ID"

# 1. Сервисный аккаунт
log "Создание сервисного аккаунта $SA_NAME..."
SA_ID=$(yc iam service-account list --folder-id "$YC_FOLDER_ID" \
    --format json | python3 -c "
import json,sys
data=json.load(sys.stdin)
sa=[x for x in data if x['name']=='$SA_NAME']
print(sa[0]['id'] if sa else '')
")

if [ -z "$SA_ID" ]; then
    SA_ID=$(yc iam service-account create \
        --name "$SA_NAME" \
        --folder-id "$YC_FOLDER_ID" \
        --format json | python3 -c "import json,sys; print(json.load(sys.stdin)['id'])")
    ok "Сервисный аккаунт создан: $SA_ID"
else
    ok "Сервисный аккаунт уже существует: $SA_ID"
fi

for ROLE in storage.admin ymq.admin serverless.functions.invoker; do
    yc resource-manager folder add-access-binding "$YC_FOLDER_ID" \
        --role "$ROLE" \
        --subject "serviceAccount:$SA_ID" \
        --quiet 2>/dev/null || true
done
ok "Роли назначены сервисному аккаунту"

KEY_FILE="$BUILD_DIR/sa-key.json"
if [ ! -f "$KEY_FILE" ]; then
    yc iam access-key create --service-account-id "$SA_ID" --folder-id "$YC_FOLDER_ID" \
        --format json > "$BUILD_DIR/sa-access-key-raw.json"
    ACCESS_KEY_ID=$(python3 -c "import json; d=json.load(open('$BUILD_DIR/sa-access-key-raw.json')); print(d['access_key']['key_id'])")
    ACCESS_KEY_SECRET=$(python3 -c "import json; d=json.load(open('$BUILD_DIR/sa-access-key-raw.json')); print(d['secret'])")
    echo "{\"key_id\":\"$ACCESS_KEY_ID\",\"secret\":\"$ACCESS_KEY_SECRET\"}" > "$KEY_FILE"
    ok "IAM ключ создан"
else
    ACCESS_KEY_ID=$(python3 -c "import json; print(json.load(open('$KEY_FILE'))['key_id'])")
    ACCESS_KEY_SECRET=$(python3 -c "import json; print(json.load(open('$KEY_FILE'))['secret'])")
    ok "IAM ключ загружен из кэша"
fi

# 2. Бакет для файлов транспортных средств
log "Создание бакета Object Storage: $STORAGE_BUCKET..."
python3 - <<PYEOF
import boto3
from botocore.exceptions import ClientError
s3 = boto3.client('s3',
    endpoint_url='https://storage.yandexcloud.net',
    region_name='ru-central1',
    aws_access_key_id='$ACCESS_KEY_ID',
    aws_secret_access_key='$ACCESS_KEY_SECRET',
)
try:
    s3.create_bucket(Bucket='$STORAGE_BUCKET')
    s3.put_bucket_acl(Bucket='$STORAGE_BUCKET', ACL='public-read')
except ClientError as e:
    code = e.response['Error']['Code']
    if code not in ('BucketAlreadyExists', 'BucketAlreadyOwnedByYou'):
        raise
PYEOF
ok "Бакет $STORAGE_BUCKET готов"

# 3. Бакет для клиентского приложения
log "Создание бакета для клиента: $CLIENT_BUCKET..."
yc storage bucket create --name "$CLIENT_BUCKET" --folder-id "$YC_FOLDER_ID" \
    --default-storage-class standard 2>/dev/null || ok "Бакет $CLIENT_BUCKET уже существует"

yc storage bucket update --name "$CLIENT_BUCKET" \
    --website-settings '{"index":"index.html","error":"index.html"}' \
    --folder-id "$YC_FOLDER_ID" 2>/dev/null || true

yc storage bucket set-https --name "$CLIENT_BUCKET" --folder-id "$YC_FOLDER_ID" 2>/dev/null || true
ok "Бакеты настроены"

# 4. Message Queue
log "Создание Message Queue: $QUEUE_NAME..."
QUEUE_URL=$(python3 - <<PYEOF
import boto3, sys
from botocore.exceptions import ClientError
sqs = boto3.client(
    'sqs',
    endpoint_url='https://message-queue.api.cloud.yandex.net',
    region_name='ru-central1',
    aws_access_key_id='$ACCESS_KEY_ID',
    aws_secret_access_key='$ACCESS_KEY_SECRET',
)
try:
    r = sqs.create_queue(QueueName='$QUEUE_NAME')
    print(r['QueueUrl'])
except ClientError as e:
    code = e.response['Error']['Code']
    if code in ('QueueAlreadyExists', 'QueueAlreadyOwnedByYou'):
        r = sqs.get_queue_url(QueueName='$QUEUE_NAME')
        print(r['QueueUrl'])
    else:
        print('', file=sys.stderr)
        print(f'MQ error: {e}', file=sys.stderr)
        sys.exit(1)
PYEOF
)
ok "Очередь готова: $QUEUE_URL"

# 5. Архив исходного кода vehicle-generator
log "Подготовка исходного кода $API_FUNCTION_NAME..."
API_SRC_DIR="$ROOT_DIR/ProjectApp.Api.Function"
rm -f "$BUILD_DIR/api-function.zip"
cd "$API_SRC_DIR"
zip -r "$BUILD_DIR/api-function.zip" \
    Handler.cs \
    ModuleInit.cs \
    Services/ \
    ProjectApp.Api.Function.csproj \
    -q
cd "$ROOT_DIR"
ok "Архив создан: $BUILD_DIR/api-function.zip ($(du -sh "$BUILD_DIR/api-function.zip" | cut -f1))"

# 6. Развёртывание Cloud Function vehicle-generator
log "Развёртывание Cloud Function $API_FUNCTION_NAME..."
yc serverless function create --name "$API_FUNCTION_NAME" \
    --folder-id "$YC_FOLDER_ID" 2>/dev/null || ok "$API_FUNCTION_NAME уже существует"

API_FUNCTION_ID=$(yc serverless function get "$API_FUNCTION_NAME" \
    --folder-id "$YC_FOLDER_ID" --format json \
    | python3 -c "import json,sys; print(json.load(sys.stdin)['id'])")

yc serverless function version create \
    --function-name "$API_FUNCTION_NAME" \
    --folder-id "$YC_FOLDER_ID" \
    --runtime dotnet8 \
    --entrypoint "ApiFunction.Handler" \
    --memory 256m \
    --execution-timeout 30s \
    --source-path "$BUILD_DIR/api-function.zip" \
    --environment "SQS_ENDPOINT=https://message-queue.api.cloud.yandex.net" \
    --environment "SQS_QUEUE_URL=$QUEUE_URL" \
    --environment "AWS_ACCESS_KEY_ID=$ACCESS_KEY_ID" \
    --environment "AWS_SECRET_ACCESS_KEY=$ACCESS_KEY_SECRET" \
    --service-account-id "$SA_ID"

yc serverless function allow-unauthenticated-invoke "$API_FUNCTION_NAME" \
    --folder-id "$YC_FOLDER_ID" 2>/dev/null || true

ok "Cloud Function $API_FUNCTION_NAME развёрнута: $API_FUNCTION_ID"

# 7. Архив исходного кода vehicle-file-service
log "Подготовка исходного кода $FILE_FUNCTION_NAME..."
FILE_SRC_DIR="$ROOT_DIR/ProjectApp.FileService.Function"
rm -f "$BUILD_DIR/file-function.zip"
cd "$FILE_SRC_DIR"
zip -r "$BUILD_DIR/file-function.zip" \
    Handler.cs \
    Models/ \
    ProjectApp.FileService.Function.csproj \
    -q
cd "$ROOT_DIR"
ok "Архив создан: $BUILD_DIR/file-function.zip ($(du -sh "$BUILD_DIR/file-function.zip" | cut -f1))"

# 8. Развёртывание Cloud Function vehicle-file-service
log "Развёртывание Cloud Function $FILE_FUNCTION_NAME..."
yc serverless function create --name "$FILE_FUNCTION_NAME" \
    --folder-id "$YC_FOLDER_ID" 2>/dev/null || ok "$FILE_FUNCTION_NAME уже существует"

FILE_FUNCTION_ID=$(yc serverless function get "$FILE_FUNCTION_NAME" \
    --folder-id "$YC_FOLDER_ID" --format json \
    | python3 -c "import json,sys; print(json.load(sys.stdin)['id'])")

yc serverless function version create \
    --function-name "$FILE_FUNCTION_NAME" \
    --folder-id "$YC_FOLDER_ID" \
    --runtime dotnet8 \
    --entrypoint "FileServiceFunction.Handler" \
    --memory 256m \
    --execution-timeout 60s \
    --source-path "$BUILD_DIR/file-function.zip" \
    --environment "S3_ENDPOINT=https://storage.yandexcloud.net" \
    --environment "S3_BUCKET=$STORAGE_BUCKET" \
    --environment "AWS_ACCESS_KEY_ID=$ACCESS_KEY_ID" \
    --environment "AWS_SECRET_ACCESS_KEY=$ACCESS_KEY_SECRET" \
    --service-account-id "$SA_ID"

ok "Cloud Function $FILE_FUNCTION_NAME развёрнута: $FILE_FUNCTION_ID"

# 9. Триггер Message Queue → vehicle-file-service
log "Создание триггера Message Queue → $FILE_FUNCTION_NAME..."
TRIGGER_EXISTS=$(yc serverless trigger list --folder-id "$YC_FOLDER_ID" --format json 2>/dev/null \
    | python3 -c "import json,sys; d=json.load(sys.stdin); print('yes' if any(x.get('name')=='vehicle-mq-trigger' for x in d) else '')" 2>/dev/null || echo "")

if [ -z "$TRIGGER_EXISTS" ]; then
    yc serverless trigger create message-queue \
        --name "vehicle-mq-trigger" \
        --folder-id "$YC_FOLDER_ID" \
        --queue "$QUEUE_URL" \
        --queue-service-account-id "$SA_ID" \
        --invoke-function-id "$FILE_FUNCTION_ID" \
        --invoke-function-service-account-id "$SA_ID" \
        --batch-size 10 \
        --batch-cutoff 10s 2>&1 | grep -v "^$" || ok "Триггер создан с предупреждением"
    ok "Триггер создан"
else
    ok "Триггер уже существует"
fi

# 10. API Gateway
log "Развёртывание API Gateway $API_GATEWAY_NAME..."
GATEWAY_SPEC="$BUILD_DIR/api-gateway-rendered.yaml"
sed -e "s|\${FUNCTION_ID}|$API_FUNCTION_ID|g" \
    -e "s|\${SERVICE_ACCOUNT_ID}|$SA_ID|g" \
    "$SCRIPT_DIR/api-gateway.yaml" > "$GATEWAY_SPEC"

GATEWAY_ID=$(yc serverless api-gateway list --folder-id "$YC_FOLDER_ID" --format json \
    | python3 -c "
import json,sys
data=json.load(sys.stdin)
gw=[x for x in data if x['name']=='$API_GATEWAY_NAME']
print(gw[0]['id'] if gw else '')
")

if [ -z "$GATEWAY_ID" ]; then
    GATEWAY_ID=$(yc serverless api-gateway create \
        --name "$API_GATEWAY_NAME" \
        --folder-id "$YC_FOLDER_ID" \
        --spec "$GATEWAY_SPEC" \
        --format json | python3 -c "import json,sys; print(json.load(sys.stdin)['id'])")
    ok "API Gateway создан: $GATEWAY_ID"
else
    yc serverless api-gateway update "$API_GATEWAY_NAME" \
        --folder-id "$YC_FOLDER_ID" \
        --spec "$GATEWAY_SPEC" 2>/dev/null || true
    ok "API Gateway обновлён: $GATEWAY_ID"
fi

GATEWAY_DOMAIN=$(yc serverless api-gateway get "$API_GATEWAY_NAME" \
    --folder-id "$YC_FOLDER_ID" --format json \
    | python3 -c "import json,sys; print(json.load(sys.stdin).get('domain',''))")

API_GATEWAY_URL="https://$GATEWAY_DOMAIN"
ok "API Gateway URL: $API_GATEWAY_URL"

# 11. Сборка и деплой клиентского приложения
log "Сборка Blazor WASM клиента..."
CLIENT_SETTINGS="$ROOT_DIR/Client.Wasm/wwwroot/appsettings.Production.json"
cat > "$CLIENT_SETTINGS" <<EOF
{
  "BaseAddress": "$API_GATEWAY_URL/"
}
EOF

dotnet publish "$ROOT_DIR/Client.Wasm/Client.Wasm.csproj" \
    -c Release -o "$BUILD_DIR/client-publish" --nologo

CLIENT_WWWROOT="$BUILD_DIR/client-publish/wwwroot"
ok "Клиент собран"

log "Загрузка клиента в Object Storage $CLIENT_BUCKET..."
python3 - <<PYEOF
import boto3, os, mimetypes
s3 = boto3.client('s3',
    endpoint_url='https://storage.yandexcloud.net',
    region_name='ru-central1',
    aws_access_key_id='$ACCESS_KEY_ID',
    aws_secret_access_key='$ACCESS_KEY_SECRET',
)
try:
    s3.create_bucket(Bucket='$CLIENT_BUCKET')
    s3.put_bucket_acl(Bucket='$CLIENT_BUCKET', ACL='public-read')
    s3.put_bucket_website(Bucket='$CLIENT_BUCKET',
        WebsiteConfiguration={'IndexDocument':{'Suffix':'index.html'},'ErrorDocument':{'Key':'index.html'}})
except Exception:
    pass
wwwroot = '$CLIENT_WWWROOT'
count = 0
for root, dirs, files in os.walk(wwwroot):
    for fname in files:
        fpath = os.path.join(root, fname)
        key = fpath[len(wwwroot)+1:]
        extra = {}
        if fname.endswith('.br'):
            base = fname[:-3]
            ct, _ = mimetypes.guess_type(base)
            if ct is None:
                ct = 'application/wasm' if base.endswith('.wasm') else 'application/octet-stream'
            extra['ContentEncoding'] = 'br'
        elif fname.endswith('.gz'):
            base = fname[:-3]
            ct, _ = mimetypes.guess_type(base)
            if ct is None:
                ct = 'application/wasm' if base.endswith('.wasm') else 'application/octet-stream'
            extra['ContentEncoding'] = 'gzip'
        else:
            ct, _ = mimetypes.guess_type(fname)
            if ct is None:
                ct = 'application/wasm' if fname.endswith('.wasm') else 'application/octet-stream'
        with open(fpath, 'rb') as f:
            s3.put_object(Bucket='$CLIENT_BUCKET', Key=key, Body=f.read(),
                          ContentType=ct, ACL='public-read', **extra)
        count += 1
print(f'Загружено файлов: {count}')
PYEOF

CLIENT_URL="http://$CLIENT_BUCKET.website.yandexcloud.net"
ok "Клиент опубликован: $CLIENT_URL"

echo ""
echo "Развёртывание завершено"
echo "  API Gateway:  $API_GATEWAY_URL"
echo "  Клиент:       $CLIENT_URL"
echo "  Очередь:      $QUEUE_URL"
echo "  Бакет файлов: $STORAGE_BUCKET"
echo "  Бакет клиента: $CLIENT_BUCKET"

cat > "$BUILD_DIR/deployment-info.json" <<EOF
{
  "apiGatewayUrl": "$API_GATEWAY_URL",
  "clientUrl": "$CLIENT_URL",
  "queueUrl": "$QUEUE_URL",
  "storageBucket": "$STORAGE_BUCKET",
  "clientBucket": "$CLIENT_BUCKET",
  "apiFunctionId": "$API_FUNCTION_ID",
  "fileFunctionId": "$FILE_FUNCTION_ID",
  "serviceAccountId": "$SA_ID"
}
EOF
