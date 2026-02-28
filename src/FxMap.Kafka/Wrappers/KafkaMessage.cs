using FxMap.Responses;

namespace FxMap.Kafka.Wrappers;

internal class KafkaMessage
{
    public bool IsSucceed { get; set; }
    public string ErrorDetail { get; set; }
    public ItemsResponse<DataResponse> Response { get; set; }
}