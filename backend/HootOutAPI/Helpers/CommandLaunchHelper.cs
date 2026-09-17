using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;

namespace HootOut.HootOutAPI.Helpers
{
    public static class CommandLaunchHelper
    {
        public static ActionResult Launch<T>(
            ILogger logger,
            Func<T> action,
            Func<object, ActionResult> badRequest,
            Func<object, ActionResult> internalError
            )
        {
            try
            {
                var result = action.Invoke();
                return new OkObjectResult(result);
            }
            catch (ValidationException ex)
            {
                logger.LogInformation(ex, ex.Message);
                return badRequest(ex.Message);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, ex.Message);
                return internalError(ex.Message);
            }
        }

        public static ActionResult Launch<T, Y>(
            ILogger logger,
            Action action,
            Func<object, ActionResult> badRequest,
            Func<object, ActionResult> internalError
            )
        {
            try
            {
                action.Invoke();
            }
            catch (ValidationException ex)
            {
                logger.LogInformation(ex.Message);
                return badRequest(ex.Message);
            }
            catch (Exception ex)
            {
                logger.LogError(ex.Message);
                return internalError(ex.Message);
            }

            return new OkResult();
        }
    }
}
