using FxMap.Abstractions;
using FxMap.Models;
using FxMap.Responses;

namespace FxMap.Grpc.Delegates;

internal delegate Func<DistributedMapRequest, IContext, Task<ItemsResponse<DataResponse>>> GetMapperResponseFunc(
    string distributedKeyAssembly);