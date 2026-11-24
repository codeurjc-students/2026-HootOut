using System;
using System.Collections.Generic;
using System.Text;

namespace HootOut.Contracts.Common.Dto
{
    public sealed record PictureDto
    {
        public Guid MediaFileId { get; set; }
        public string Url { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public string ContentType { get; set; } = string.Empty;
    }
}
