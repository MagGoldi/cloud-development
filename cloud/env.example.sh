# Скопируйте этот файл в env.sh и заполните значениями из вашего Yandex Cloud аккаунта

# Идентификатор облака (yc config get cloud-id)
export YC_CLOUD_ID="b1g..."

# Идентификатор каталога (yc config get folder-id)
export YC_FOLDER_ID="b1g..."

# Имя сервисного аккаунта
export SA_NAME="vehicle-sa"

# Имя бакета для файлов транспортных средств
export STORAGE_BUCKET="vehicles-storage"

# Имя бакета для клиентского приложения
export CLIENT_BUCKET="vehicles-client"

# Имя очереди сообщений
export QUEUE_NAME="vehicle-queue"

# Имя Cloud Function для генерации
export API_FUNCTION_NAME="vehicle-generator"

# Имя Cloud Function для файлового сервиса
export FILE_FUNCTION_NAME="vehicle-file-service"

# Имя API Gateway
export API_GATEWAY_NAME="vehicle-api-gateway"

# Регион YC (по умолчанию ru-central1)
export YC_REGION="ru-central1"
