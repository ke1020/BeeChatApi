using System;
using System.IO;
using System.Threading.Tasks;
using Ke.Chat.Uploads;
using Ke.Storage.Abstractions;
using Ke.Storage.Models;
using Ke.Storage.Models.Local;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;

namespace Ke.Chat.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UploadController(IOptions<StorageOptions> options, IFileValidator fileValidator) : AbpController
{
    private readonly LocalStorageOptions _localStorageOptions = options.Value.Local ??
        throw new UserFriendlyException("Local storage options not found.")
        ;
    private readonly IFileValidator _fileValidator = fileValidator;

    /// <summary>
    /// 上传文件
    /// </summary>
    /// <param name="file"></param>
    /// <returns></returns>
    /// <exception cref="UserFriendlyException"></exception>
    [HttpPost("")]
    [Consumes("multipart/form-data")]
    public virtual async Task<ActionResult<FileUploadResultDto>> UploadAsync(IFormFile file)
    {
        using var stream = file.OpenReadStream();

        // 验证文件
        var validateR = await _fileValidator.ValidateAsync(stream,
            file.FileName,
            file.ContentType)
            ;
        if (!validateR.IsValid)
        {
            throw new UserFriendlyException(validateR.ErrorMessage ?? string.Empty);
        }

        // 生成唯一文件名
        var fileExtension = Path.GetExtension(file.FileName);
        var uniqueFileName = $"{Guid.NewGuid():N}{fileExtension}";

        // 获取存储路径配置
        var uploadPath = Path.Combine(_localStorageOptions.TempDirectory ?? "temp");
        if (!Path.IsPathRooted(uploadPath))
        {
            uploadPath = Path.Combine(Directory.GetCurrentDirectory(), uploadPath);
        }

        // 确保目录存在
        if (!Directory.Exists(uploadPath))
        {
            Directory.CreateDirectory(uploadPath);
        }

        var filePath = Path.Combine(uploadPath, uniqueFileName);
        var serverPath = $"{_localStorageOptions.TempDirectory?.Replace('\\', '/').Trim('/')}/{uniqueFileName}";

        // 保存文件
        using var fileStream = new FileStream(filePath, FileMode.Create);
        await stream.CopyToAsync(fileStream);

        return new FileUploadResultDto(file.FileName,
            file.Length,
            serverPath,
            file.ContentType,
            DateTime.UtcNow)
            ;
    }
}