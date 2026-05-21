using System.Text.Json.Serialization;

namespace FileServiceFunction.Models;

public class QueueTriggerEvent
{
    [JsonPropertyName("messages")]
    public List<QueueMessage> Messages { get; set; } = [];
}

public class QueueMessage
{
    [JsonPropertyName("event_metadata")]
    public EventMetadata EventMetadata { get; set; } = new();

    [JsonPropertyName("details")]
    public MessageDetails Details { get; set; } = new();
}

public class EventMetadata
{
    [JsonPropertyName("event_id")]
    public string EventId { get; set; } = "";

    [JsonPropertyName("event_type")]
    public string EventType { get; set; } = "";

    [JsonPropertyName("created_at")]
    public string CreatedAt { get; set; } = "";
}

public class MessageDetails
{
    [JsonPropertyName("queue_id")]
    public string QueueId { get; set; } = "";

    [JsonPropertyName("message")]
    public SqsMessage Message { get; set; } = new();
}

public class SqsMessage
{
    [JsonPropertyName("message_id")]
    public string MessageId { get; set; } = "";

    [JsonPropertyName("md5_of_body")]
    public string Md5OfBody { get; set; } = "";

    [JsonPropertyName("body")]
    public string Body { get; set; } = "";

    [JsonPropertyName("attributes")]
    public Dictionary<string, string>? Attributes { get; set; }
}
