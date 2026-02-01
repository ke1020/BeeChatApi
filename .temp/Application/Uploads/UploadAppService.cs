using Volo.Abp.Application.Services;
using Microsoft.Extensions.Logging;
using Ke.Storage.Abstractions;

namespace Ke.Chat.Uploads;

public class UploadAppService : ApplicationService, IUploadAppService
{
    private readonly ILogger<UploadAppService> _logger;
    private readonly ILocalStorageManager _localStorageManager;

    public UploadAppService(ILogger<UploadAppService> logger, IStorageFactory storageFactory)
    {
        _logger = logger;
        _localStorageManager = storageFactory.CreateLocal();
    }

    /*
    public virtual async Task<IRemoteStreamContent> DownloadAsync(Guid id)
    {
        var file = await _uploadedFileRepository.GetAsync(id);

        if (!System.IO.File.Exists(file.FilePath))
        {
            throw new UserFriendlyException("文件不存在或已被删除");
        }

        var fileStream = new FileStream(file.FilePath, FileMode.Open, FileAccess.Read);

        return new RemoteStreamContent(fileStream)
        {
            ContentType = file.ContentType,
            FileName = file.FileName
        };
    }
    

    public virtual async Task DeleteAsync(Guid id)
    {
        var file = await _uploadedFileRepository.GetAsync(id);

        // 删除物理文件
        if (System.IO.File.Exists(file.FilePath))
        {
            System.IO.File.Delete(file.FilePath);
        }

        // 删除数据库记录
        await _uploadedFileRepository.DeleteAsync(id);
    }

    public virtual async Task<FileUploadResultDto> GetAsync(Guid id)
    {
        var file = await _uploadedFileRepository.GetAsync(id);

        var baseUrl = _configuration["App:SelfUrl"] ?? $"{Request.Scheme}://{Request.Host}";
        var downloadUrl = $"{baseUrl}/api/upload/download/{file.Id}";

        return new FileUploadResultDto
        {
            Id = file.Id,
            FileName = file.FileName,
            FileSize = file.FileSize,
            FilePath = file.FilePath,
            ContentType = file.ContentType,
            CreationTime = file.CreationTime,
            DownloadUrl = downloadUrl
        };
    }
    */
}
