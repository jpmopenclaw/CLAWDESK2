using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CLAWDESK.Models;

namespace CLAWDESK.Services
{
    public class FileService
    {
        private readonly ConfigService _configService;

        public FileService(ConfigService configService)
        {
            _configService = configService;
        }

        public string GetWorkspacePath() => _configService.GetWorkspacePath();

        public async Task<List<FileItem>> GetFilesAsync(string? path = null)
        {
            return await Task.Run(() =>
            {
                var workspacePath = path ?? _configService.GetWorkspacePath();
                if (!Directory.Exists(workspacePath))
                    return new List<FileItem>();

                var files = new List<FileItem>();
                try
                {
                    var directories = Directory.GetDirectories(workspacePath);
                    var regularFiles = Directory.GetFiles(workspacePath);

                    foreach (var dir in directories)
                    {
                        var info = new DirectoryInfo(dir);
                        files.Add(new FileItem
                        {
                            Name = info.Name,
                            FullPath = info.FullName,
                            IsDirectory = true,
                            LastModified = info.LastWriteTime
                        });
                    }

                    foreach (var file in regularFiles)
                    {
                        var info = new FileInfo(file);
                        files.Add(new FileItem
                        {
                            Name = info.Name,
                            FullPath = info.FullName,
                            IsDirectory = false,
                            LastModified = info.LastWriteTime
                        });
                    }
                }
                catch (UnauthorizedAccessException)
                {
                    // 權限不足，跳過
                }

                return files.OrderBy(f => !f.IsDirectory).ThenBy(f => f.Name).ToList();
            });
        }

        public async Task<string> ReadFileContentAsync(string filePath)
        {
            if (!File.Exists(filePath))
                return "檔案不存在";

            var extension = Path.GetExtension(filePath).ToLower();
            var textExtensions = new[] { ".txt", ".md", ".json", ".xml", ".cs", ".xaml", ".html", ".css", ".js", ".ts", ".yaml", ".yml" };

            if (!textExtensions.Contains(extension))
            {
                return $"[無法預覽] 檔案類型: {extension}";
            }

            try
            {
                return await File.ReadAllTextAsync(filePath);
            }
            catch (Exception ex)
            {
                return $"[錯誤] {ex.Message}";
            }

        public async Task<bool> WriteFileContentAsync(string filePath, string content)
        {
            try
            {
                await System.IO.File.WriteAllTextAsync(filePath, content);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
