
using System.IO;

namespace Ke.Chat.Uploads;

public record FileUploadInputDto(string FileName, Stream Stream, string ContentType);