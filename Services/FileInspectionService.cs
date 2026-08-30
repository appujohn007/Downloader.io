using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace DownloaderApp.Services;

public class FileInspectionResult
{
    public string MagicBytesHex { get; set; } = string.Empty;
    public string MagicByteType { get; set; } = "Unknown Binary Data";
    public string Category { get; set; } = "Other";
    public string TypeSpecificDetails { get; set; } = string.Empty;
    public string StorageDriveInfo { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; } = 0;
    public DateTime? FileLastModified { get; set; }
}

public class HashInspectionResult
{
    public string Md5 { get; set; } = string.Empty;
    public string Sha1 { get; set; } = string.Empty;
    public string Sha256 { get; set; } = string.Empty;
    public string Sha512 { get; set; } = string.Empty;
    public string Crc32 { get; set; } = string.Empty;
}

public static class FileInspectionService
{
    public static FileInspectionResult InspectFile(string filePath)
    {
        var result = new FileInspectionResult();
        if (string.IsNullOrWhiteSpace(filePath)) return result;

        try
        {
            var fileInfo = new FileInfo(filePath);
            if (!fileInfo.Exists)
            {
                var partial = filePath + ".partial";
                if (File.Exists(partial)) fileInfo = new FileInfo(partial);
                else return result;
            }

            result.FileSizeBytes = fileInfo.Length;
            result.FileLastModified = fileInfo.LastWriteTime;

            // Target Storage Drive Info
            try
            {
                var root = Path.GetPathRoot(fileInfo.FullName);
                if (!string.IsNullOrEmpty(root))
                {
                    var drive = new DriveInfo(root);
                    if (drive.IsReady)
                    {
                        double freeGb = drive.AvailableFreeSpace / (1024.0 * 1024.0 * 1024.0);
                        double totalGb = drive.TotalSize / (1024.0 * 1024.0 * 1024.0);
                        result.StorageDriveInfo = $"{drive.Name} ({drive.DriveFormat}, {drive.DriveType} • {freeGb:F1} GB free of {totalGb:F1} GB)";
                    }
                }
            }
            catch { }

            // Read first 4KB for header inspection
            byte[] header = new byte[Math.Min(4096, (int)fileInfo.Length)];
            if (header.Length > 0)
            {
                using (var fs = new FileStream(fileInfo.FullName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    int read = fs.Read(header, 0, header.Length);
                    if (read < header.Length) Array.Resize(ref header, read);
                }
            }

            if (header.Length == 0)
            {
                result.MagicByteType = "Empty File (0 Bytes)";
                return result;
            }

            // Extract Hex representation of first 8-16 bytes
            int hexLen = Math.Min(12, header.Length);
            var sbHex = new StringBuilder();
            for (int i = 0; i < hexLen; i++)
            {
                sbHex.Append(header[i].ToString("X2"));
                if (i < hexLen - 1) sbHex.Append(' ');
            }
            result.MagicBytesHex = sbHex.ToString();

            // Detect format from magic bytes
            DetectFormat(header, fileInfo.FullName, result);
        }
        catch (Exception ex)
        {
            result.MagicByteType = "Inspection Error: " + ex.Message;
        }

        return result;
    }

    private static void DetectFormat(byte[] header, string fullPath, FileInspectionResult result)
    {
        // 1. PDF Document
        if (header.Length >= 5 && header[0] == 0x25 && header[1] == 0x50 && header[2] == 0x44 && header[3] == 0x46 && header[4] == 0x2D) // %PDF-
        {
            result.Category = "Documents";
            string version = "1.x";
            if (header.Length >= 8)
            {
                version = Encoding.ASCII.GetString(header, 5, Math.Min(4, header.Length - 5)).Trim();
            }
            result.MagicByteType = $"PDF Document (v{version})";

            int pageCount = EstimatePdfPages(fullPath);
            result.TypeSpecificDetails = pageCount > 0 ? $"{pageCount} Pages • Standard PDF" : "Standard PDF Format";
            return;
        }

        // 2. PNG Image
        if (header.Length >= 8 && header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47 &&
            header[4] == 0x0D && header[5] == 0x0A && header[6] == 0x1A && header[7] == 0x0A)
        {
            result.Category = "Images";
            result.MagicByteType = "PNG Image (Portable Network Graphics)";
            if (header.Length >= 24)
            {
                int width = (header[16] << 24) | (header[17] << 16) | (header[18] << 8) | header[19];
                int height = (header[20] << 24) | (header[21] << 16) | (header[22] << 8) | header[23];
                int bitDepth = header[24];
                result.TypeSpecificDetails = $"{width} × {height} px • {bitDepth}-bit Color";
            }
            return;
        }

        // 3. JPEG Image
        if (header.Length >= 3 && header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF)
        {
            result.Category = "Images";
            result.MagicByteType = "JPEG Image";
            result.TypeSpecificDetails = "Lossy JPEG Photo Stream";
            return;
        }

        // 4. GIF Image
        if (header.Length >= 6 && header[0] == 0x47 && header[1] == 0x49 && header[2] == 0x46 && header[3] == 0x38 && (header[4] == 0x37 || header[4] == 0x39) && header[5] == 0x61)
        {
            result.Category = "Images";
            int width = header[6] | (header[7] << 8);
            int height = header[8] | (header[9] << 8);
            result.MagicByteType = $"GIF Image ({Encoding.ASCII.GetString(header, 0, 6)})";
            result.TypeSpecificDetails = $"{width} × {height} px • Animated Graphics";
            return;
        }

        // 5. WebP Image
        if (header.Length >= 12 && header[0] == 0x52 && header[1] == 0x49 && header[2] == 0x46 && header[3] == 0x46 &&
            header[8] == 0x57 && header[9] == 0x45 && header[10] == 0x42 && header[11] == 0x50)
        {
            result.Category = "Images";
            result.MagicByteType = "WebP Image (Google WebP)";
            result.TypeSpecificDetails = "Modern WebP Container";
            return;
        }

        // 6. ZIP / Office DOCX / APK / JAR Archive
        if (header.Length >= 4 && header[0] == 0x50 && header[1] == 0x4B && (header[2] == 0x03 || header[2] == 0x05 || header[2] == 0x07) && (header[3] == 0x04 || header[3] == 0x06 || header[3] == 0x08))
        {
            result.Category = "Compressed";
            string ext = Path.GetExtension(fullPath).ToLowerInvariant();
            if (ext == ".docx" || ext == ".xlsx" || ext == ".pptx")
            {
                result.Category = "Documents";
                result.MagicByteType = "Office OpenXML Document";
                result.TypeSpecificDetails = "Zipped XML Package";
            }
            else if (ext == ".apk")
            {
                result.Category = "Applications";
                result.MagicByteType = "Android APK Package";
                result.TypeSpecificDetails = "Signed Android Application";
            }
            else if (ext == ".jar")
            {
                result.Category = "Applications";
                result.MagicByteType = "Java Archive (JAR)";
                result.TypeSpecificDetails = "Java Bytecode Package";
            }
            else
            {
                result.MagicByteType = "ZIP Compressed Archive";
                result.TypeSpecificDetails = "PKWare Zip Container (Deflate)";
            }
            return;
        }

        // 7. 7-Zip Archive
        if (header.Length >= 6 && header[0] == 0x37 && header[1] == 0x7A && header[2] == 0xBC && header[3] == 0xAF && header[4] == 0x27 && header[5] == 0x1C)
        {
            result.Category = "Compressed";
            result.MagicByteType = "7-Zip High-Compression Archive";
            result.TypeSpecificDetails = "LZMA / LZMA2 Container";
            return;
        }

        // 8. RAR Archive
        if (header.Length >= 7 && header[0] == 0x52 && header[1] == 0x61 && header[2] == 0x72 && header[3] == 0x21 && header[4] == 0x1A && header[5] == 0x07)
        {
            result.Category = "Compressed";
            result.MagicByteType = "RAR Archive";
            result.TypeSpecificDetails = "WinRAR Archive Stream";
            return;
        }

        // 9. GZIP Archive
        if (header.Length >= 2 && header[0] == 0x1F && header[1] == 0x8B)
        {
            result.Category = "Compressed";
            result.MagicByteType = "GZip Compressed Stream (.gz)";
            result.TypeSpecificDetails = "GNU Zip Container";
            return;
        }

        // 10. MP4 / MOV Video Container
        if (header.Length >= 12 && header[4] == 0x66 && header[5] == 0x74 && header[6] == 0x79 && header[7] == 0x70) // 'ftyp'
        {
            result.Category = "Video";
            string brand = Encoding.ASCII.GetString(header, 8, 4);
            result.MagicByteType = $"MPEG-4 Video (ftyp/{brand})";
            result.TypeSpecificDetails = "ISO Base Media File Format (MP4)";
            return;
        }

        // 11. MKV / WebM Video Container
        if (header.Length >= 4 && header[0] == 0x1A && header[1] == 0x45 && header[2] == 0xDF && header[3] == 0xA3)
        {
            result.Category = "Video";
            result.MagicByteType = "Matroska / WebM Media Container";
            result.TypeSpecificDetails = "EBML Extensible Media Stream";
            return;
        }

        // 12. Audio: MP3
        if (header.Length >= 3 && ((header[0] == 0x49 && header[1] == 0x44 && header[2] == 0x33) || (header[0] == 0xFF && (header[1] & 0xE0) == 0xE0)))
        {
            result.Category = "Audio";
            result.MagicByteType = "MPEG Audio Layer 3 (MP3)";
            result.TypeSpecificDetails = "ID3 Tagged Digital Audio";
            return;
        }

        // 13. Audio: FLAC
        if (header.Length >= 4 && header[0] == 0x66 && header[1] == 0x4C && header[2] == 0x61 && header[3] == 0x43) // 'fLaC'
        {
            result.Category = "Audio";
            result.MagicByteType = "FLAC Lossless Audio";
            result.TypeSpecificDetails = "High-Fidelity 24-bit/96kHz Lossless";
            return;
        }

        // 14. Audio/Video: RIFF WAV / AVI
        if (header.Length >= 12 && header[0] == 0x52 && header[1] == 0x49 && header[2] == 0x46 && header[3] == 0x46)
        {
            string format = Encoding.ASCII.GetString(header, 8, 4);
            if (format == "WAVE")
            {
                result.Category = "Audio";
                result.MagicByteType = "WAV Waveform Audio";
                result.TypeSpecificDetails = "Uncompressed PCM Audio";
            }
            else if (format == "AVI ")
            {
                result.Category = "Video";
                result.MagicByteType = "Audio Video Interleave (AVI)";
                result.TypeSpecificDetails = "RIFF Media Container";
            }
            return;
        }

        // 15. Windows PE Executable / DLL
        if (header.Length >= 2 && header[0] == 0x4D && header[1] == 0x5A) // 'MZ'
        {
            result.Category = "Applications";
            string arch = "x86 / x64";
            if (header.Length >= 0x40)
            {
                int peOffset = BitConverter.ToInt32(header, 0x3C);
                if (peOffset > 0 && peOffset + 6 < header.Length)
                {
                    ushort machine = BitConverter.ToUInt16(header, peOffset + 4);
                    if (machine == 0x8664) arch = "64-bit AMD64/x86_64";
                    else if (machine == 0x14C) arch = "32-bit x86 (i386)";
                    else if (machine == 0xAA64) arch = "64-bit ARM64";
                }
            }
            result.MagicByteType = $"Windows Portable Executable ({arch})";
            result.TypeSpecificDetails = "Native Windows Binary (PE / COFF)";
            return;
        }

        // 16. Linux ELF Executable
        if (header.Length >= 4 && header[0] == 0x7F && header[1] == 0x45 && header[2] == 0x4C && header[3] == 0x46) // \x7FELF
        {
            result.Category = "Applications";
            bool is64 = header.Length > 4 && header[4] == 2;
            result.MagicByteType = $"Linux Executable & Linkable Format (ELF {(is64 ? "64-bit" : "32-bit")})";
            result.TypeSpecificDetails = "POSIX Native Binary";
            return;
        }

        // 17. Plain Text / JSON / HTML / XML
        string textSnippet = Encoding.UTF8.GetString(header, 0, Math.Min(256, header.Length));
        if (textSnippet.StartsWith("<!DOCTYPE html", StringComparison.OrdinalIgnoreCase) || textSnippet.Contains("<html", StringComparison.OrdinalIgnoreCase))
        {
            result.Category = "Documents";
            result.MagicByteType = "HTML Web Page Document";
            result.TypeSpecificDetails = "Hypertext Markup Document";
            return;
        }
        if (textSnippet.TrimStart().StartsWith("{") || textSnippet.TrimStart().StartsWith("["))
        {
            result.Category = "Documents";
            result.MagicByteType = "JSON Structured Data";
            result.TypeSpecificDetails = "JavaScript Object Notation";
            return;
        }
        if (textSnippet.StartsWith("<?xml", StringComparison.OrdinalIgnoreCase))
        {
            result.Category = "Documents";
            result.MagicByteType = "XML Structured Document";
            result.TypeSpecificDetails = "Extensible Markup Language";
            return;
        }

        // Fallback by extension
        string extension = Path.GetExtension(fullPath).ToUpperInvariant().TrimStart('.');
        result.MagicByteType = !string.IsNullOrEmpty(extension) ? $"{extension} Binary Stream" : "Generic Binary File";
        result.TypeSpecificDetails = "Unidentified Binary Stream";
    }

    private static int EstimatePdfPages(string fullPath)
    {
        try
        {
            using (var fs = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var reader = new StreamReader(fs, Encoding.ASCII, false, 8192))
            {
                string content = reader.ReadToEnd();
                var match = Regex.Match(content, @"/Type\s*/Pages.*?/Count\s+(\d+)", RegexOptions.Singleline);
                if (match.Success && int.TryParse(match.Groups[1].Value, out int count))
                {
                    return count;
                }
                var pageMatches = Regex.Matches(content, @"/Type\s*/Page\b");
                if (pageMatches.Count > 0) return pageMatches.Count;
            }
        }
        catch { }
        return 0;
    }

    public static async Task<HashInspectionResult> ComputeAllHashesAsync(string filePath, CancellationToken ct = default)
    {
        var result = new HashInspectionResult();
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath)) return result;

        await Task.Run(() =>
        {
            using var sha256 = SHA256.Create();
            using var sha1 = SHA1.Create();
            using var md5 = MD5.Create();
            using var sha512 = SHA512.Create();
            uint crc = 0xFFFFFFFF;

            uint[] crcTable = new uint[256];
            for (uint i = 0; i < 256; i++)
            {
                uint c = i;
                for (int j = 0; j < 8; j++)
                {
                    c = ((c & 1) != 0) ? (0xEDB88320 ^ (c >> 1)) : (c >> 1);
                }
                crcTable[i] = c;
            }

            byte[] buffer = new byte[1024 * 1024]; // 1MB buffer for fast sequential disk I/O

            using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, buffer.Length, FileOptions.SequentialScan);
            int bytesRead;

            while ((bytesRead = fs.Read(buffer, 0, buffer.Length)) > 0)
            {
                ct.ThrowIfCancellationRequested();

                sha256.TransformBlock(buffer, 0, bytesRead, null, 0);
                sha1.TransformBlock(buffer, 0, bytesRead, null, 0);
                md5.TransformBlock(buffer, 0, bytesRead, null, 0);
                sha512.TransformBlock(buffer, 0, bytesRead, null, 0);

                for (int i = 0; i < bytesRead; i++)
                {
                    crc = crcTable[(crc ^ buffer[i]) & 0xFF] ^ (crc >> 8);
                }
            }

            sha256.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
            sha1.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
            md5.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
            sha512.TransformFinalBlock(Array.Empty<byte>(), 0, 0);

            result.Sha256 = Convert.ToHexString(sha256.Hash!).ToLowerInvariant();
            result.Sha1 = Convert.ToHexString(sha1.Hash!).ToLowerInvariant();
            result.Md5 = Convert.ToHexString(md5.Hash!).ToLowerInvariant();
            result.Sha512 = Convert.ToHexString(sha512.Hash!).ToLowerInvariant();
            result.Crc32 = (crc ^ 0xFFFFFFFF).ToString("X8");
        }, ct);

        return result;
    }
}
