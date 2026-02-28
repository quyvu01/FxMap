using FxMap.Abstractions;
using FxMap.Models;
using FxMap.Responses;

namespace FxMap.Grpc.Delegates;

public delegate Func<FxMapRequest, IContext, Task<ItemsResponse<DataResponse>>> GetFxMapResponseFunc(Type attributeType);