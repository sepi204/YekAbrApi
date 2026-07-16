using Microsoft.AspNetCore.Mvc;
using YekAbr.Api.Models.Common;
using YekAbr.Services.Common.Responses;

namespace YekAbr.Api.Extensions;

public static class ControllerExtensions
{
    public static ActionResult<ApiResponse<T>> ToApiResponse<T>(this ControllerBase controller, Result<T> result)
    {
        if (result.Success)
        {
            return controller.Ok(ApiResponse<T>.FromResult(true, result.Message, result.Data));
        }

        if (result.Errors is not null)
        {
            return controller.BadRequest(ApiResponse<T>.FromResult(false, result.Message, errors: result.Errors));
        }

        return controller.BadRequest(ApiResponse<T>.FromResult(false, result.Message));
    }
    public static ActionResult<ApiResponse<object>> ToApiResponse(this ControllerBase controller, Result<object> result)
    {
        if (result.Success)
        {
            return controller.Ok(ApiResponse<object>.FromResult(true, result.Message, result.Data));
        }

        if (result.Errors is not null)
        {
            return controller.BadRequest(ApiResponse<object>.FromResult(false, result.Message, errors: result.Errors));
        }

        return controller.BadRequest(ApiResponse<object>.FromResult(false, result.Message));
    }
}
