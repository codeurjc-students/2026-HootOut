using HootOut.CommonDomain.FileStorage;
using HootOut.Contracts.MediaFile.Responses;
using HootOut.HootOutAPI.Helpers;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace HootOut.HootOutAPI.Controllers
{
    [ApiController]
    [Route("api/v1/[controller]")]
    public class FilesController : ControllerBase
    {
        private readonly ILogger<FilesController> logger;
        private readonly IFileStorageService fileStorageService;

        private readonly FileClassifier fileClassifier;

        public FilesController(
            ILogger<FilesController> logger,
            IFileStorageService fileStorageService,
            FileClassifier fileClassifier)
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.fileStorageService = fileStorageService ?? throw new ArgumentNullException(nameof(fileStorageService));
            this.fileClassifier = fileClassifier ?? throw new ArgumentNullException(nameof(fileClassifier));
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

            var mimeType = await fileClassifier.GetMimeTypeAsync(file, ct);
            if (mimeType is null || !UploadPolicy.AllowedTypes.TryGetValue(mimeType, out var allowedCategory))
                return BadRequest("File type not allowed.");  

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
