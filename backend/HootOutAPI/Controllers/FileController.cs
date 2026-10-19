using FluentValidation;
using HootOut.CommonDomain.FileStorage;
using HootOut.Contracts.MediaFile.Dtos;
using HootOut.Contracts.MediaFile.Responses;
using HootOut.HootOutAPI.Helpers;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace HootOut.HootOutAPI.Controllers
{
    [ApiController]
    [Route("api/v1/[controller]")]
    public class FileController : ControllerBase
    {
        private readonly ILogger<FileController> logger;
        private readonly IFileStorageService fileStorageService;

        public FileController(
            ILogger<FileController> logger,
            IFileStorageService fileStorageService)
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.fileStorageService = fileStorageService ?? throw new ArgumentNullException(nameof(fileStorageService));
        }

        [HttpPost]
        public async Task<ActionResult<UploadFileResponse>> Upload(IFormFile file, CancellationToken ct)
        {
            if (file == null || file.Length == 0)
            {
                return BadRequest();
            }

            var userId = User.FindFirstValue("sub");
            if (userId is null)
                return Unauthorized();

            await using var stream = file.OpenReadStream();

            return await CommandLaunchHelper.LaunchAsync(
                logger,
                async () => await fileStorageService.UploadFileAsync(Path.GetFileName(file.FileName), stream, file.ContentType, Guid.Parse(userId), ct),
                BadRequest,
                (err) => StatusCode(StatusCodes.Status500InternalServerError, err)
            );
        }
    }
}
