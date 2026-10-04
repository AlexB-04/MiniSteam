using Microsoft.AspNetCore.Mvc;
using MiniSteam.Services;

namespace MiniSteam.Helpers
{
    public static class ApiProblemExtensions
    {
        public static ObjectResult ApiProblem(
            this ControllerBase controller,
            int statusCode,
            string title,
            string? detail = null,
            string? code = null)
        {
            var problem = new ProblemDetails
            {
                Status = statusCode,
                Title = title,
                Detail = detail,
                Instance = controller.HttpContext.Request.Path
            };

            problem.Extensions["traceId"] = controller.HttpContext.TraceIdentifier;

            if (!string.IsNullOrWhiteSpace(code))
            {
                problem.Extensions["code"] = code;
            }

            var result = new ObjectResult(problem)
            {
                StatusCode = statusCode
            };

            result.ContentTypes.Add("application/problem+json");
            return result;
        }

        public static IActionResult FromServiceFailure<T>(
            this ControllerBase controller,
            ServiceResult<T> result,
            string fallbackTitle)
        {
            var statusCode = result.Status switch
            {
                ServiceResultStatus.NotFound => StatusCodes.Status404NotFound,
                ServiceResultStatus.Forbidden => StatusCodes.Status403Forbidden,
                ServiceResultStatus.AlreadyOwned => StatusCodes.Status409Conflict,
                ServiceResultStatus.Conflict => StatusCodes.Status409Conflict,
                ServiceResultStatus.Empty => StatusCodes.Status400BadRequest,
                ServiceResultStatus.InvalidOperation => StatusCodes.Status409Conflict,
                _ => StatusCodes.Status400BadRequest
            };

            return controller.ApiProblem(
                statusCode,
                fallbackTitle,
                result.Message,
                result.Status.ToString());
        }
    }
}
