using System;

namespace Ke.Chat.Uploads;

public record FileUploadResultDto(string FileName, 
    long FileSize, 
    string ServerPath, 
    string ContentType, 
    DateTime CreationTime)
    ;
