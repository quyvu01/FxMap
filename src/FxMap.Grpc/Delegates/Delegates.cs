using FxMap.Abstractions;
using FxMap.Models;
using FxMap.Responses;

namespace FxMap.Grpc.Delegates;

public delegate Func<DistributedMapRequest, IContext, Task<ItemsResponse<DataResponse>>> GetMapperResponseFunc(
    string distributedKeyAssembly);