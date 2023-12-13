using System;
using System.Diagnostics;
using System.Drawing.Imaging;
using System.Drawing;
using System.IO;
using System.Text;
using MetadataExtractor;
using MetadataExtractor.Formats.Exif;
using MetadataExtractor.Formats.Jpeg;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Metadata;
using System.Runtime.Serialization;
using System.Collections;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Formats;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Formats.Bmp;
using SixLabors.ImageSharp.Advanced;

class MetadataInfo
{
    public string? Name { get; set; }
    public string? Description { get; set; }
}

class Program
{
    static void Main(string[] args)
    {
        string currentDirectory = System.IO.Directory.GetCurrentDirectory();
        string parentDirectory = Path.Combine(currentDirectory, "..", "..", "..", "..");
        string path = Path.Combine(parentDirectory, "Images");

        List<MetadataInfo> headerMetadata;

        Console.Clear();

        if (System.IO.Directory.Exists(path))
        {
            // Get all files with the .jpeg/.jpg extension in the specified directory
            string[] jpegFiles1 = System.IO.Directory.GetFiles(path, "*.jpeg");
            string[] jpegFiles2 = System.IO.Directory.GetFiles(path, "*.jpg");
            string[] jpegFiles = jpegFiles1.Concat(jpegFiles2).ToArray();

            if (jpegFiles.Any())
            {

                // Create a folder for corrupted images
                string corruptedImagesFolderPath = Path.Combine(path, "Corrupted Images");
                if (!System.IO.Directory.Exists(corruptedImagesFolderPath))
                {
                    System.IO.Directory.CreateDirectory(corruptedImagesFolderPath);
                }

                // Process each image
                foreach (string jpegFile in jpegFiles)
                {
                    try
                    {
                        headerMetadata = new List<MetadataInfo>();

                        // Use ImageSharp to load the image and get the header bytes
                        using (var image = SixLabors.ImageSharp.Image.Load(jpegFile))
                        {
                            // Convert the image to a byte array
                            using (var stream = new MemoryStream())
                            {
                                image.Save(stream, new SixLabors.ImageSharp.Formats.Jpeg.JpegEncoder());
                                byte[] fileBytes = stream.ToArray();


                                // Find the end of the header (start of the image data)
                                int headerEndPosition = -1;
                                for (int i = 0; i < fileBytes.Length - 1; i++)
                                {
                                    if (fileBytes[i] == 0xFF && fileBytes[i + 1] == 0xDA)
                                    {
                                        headerEndPosition = i; // Found the marker for the start of the image data
                                        break;
                                    }
                                }

                                // Make sure the headerEndPosition is valid
                                if (headerEndPosition == -1 || headerEndPosition >= fileBytes.Length)
                                {
                                    Console.WriteLine("Invalid headerEndPosition");
                                    continue; // Skip to the next image
                                }

                                headerEndPosition += 2;

                                // Split the file into header and the rest of the file
                                byte[] headerBytes = fileBytes.Take(headerEndPosition).ToArray();
                                byte[] bodyFileBytes = fileBytes.Skip(headerEndPosition).ToArray();

                                int imageWidth = 0;
                                int imageHeight = 0;

                                // Use JpegMetadataReader to extract metadata from the header
                                var directories = JpegMetadataReader.ReadMetadata(new MemoryStream(headerBytes));

                                Console.WriteLine($"Metadata for {Path.GetFileName(jpegFile)}:");

                                foreach (var directory in directories)
                                {
                                    foreach (var tag in directory.Tags)
                                    {
                                        // Print metadata information
                                        Console.WriteLine($"{tag.Name}: {tag.Description}");

                                        // Save metadata information
                                        var metadataInfo = new MetadataInfo
                                        {
                                            Name = tag.Name,
                                            Description = tag.Description
                                        };
                                        headerMetadata.Add(metadataInfo);

                                        // Retrieve width and height from the metadata in the header
                                        if (tag.Name == "Image Width")
                                        {
                                            if (int.TryParse(tag.Description?.Split(' ')[0], out int width))
                                            {
                                                imageWidth = width;
                                            }
                                        }
                                        else if (tag.Name == "Image Height")
                                        {
                                            if (int.TryParse(tag.Description?.Split(' ')[0], out int height))
                                            {
                                                imageHeight = height;
                                            }
                                        }
                                    }
                                }
                                Console.WriteLine();

                                Random random = new Random();
                                // Generate random indices
                                List<int> indicesToModify = new List<int>();
                                for (int i = 0; i < 400; i++)
                                {
                                    int randomIndex = random.Next(bodyFileBytes.Length);
                                    indicesToModify.Add(randomIndex);
                                }

                                // Modify the bytes at the randomly selected indices
                                foreach (int index in indicesToModify)
                                {
                                    // Add a check so that the end of image marker is not modified
                                    if (bodyFileBytes[index] != 0xFF || bodyFileBytes[index] != 0xD9)
                                    {
                                        bodyFileBytes[index] = 0x00;
                                    }

                                }

                                // Save the bitmap as a BMP file
                                string outputFilePath = Path.Combine(corruptedImagesFolderPath, "Corrupted_" + Path.GetFileNameWithoutExtension(jpegFile) + ".bmp");                              

                                byte[] combined = CombineBytes(headerBytes, bodyFileBytes);

                                var bmp = new Bitmap(new MemoryStream(combined));
                                bmp.Save(outputFilePath);
                                bmp.Dispose();

                                try
                                {
                                    // Use Process.Start to open the default image viewer for the BMP file
                                    Process.Start(new ProcessStartInfo
                                    {
                                        FileName = outputFilePath,
                                        UseShellExecute = true
                                    });
                                }
                                catch (Exception ex)
                                {
                                    Console.WriteLine($"Error opening image: {ex.Message}");
                                }

                            }
                        }

                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error processing file {Path.GetFileName(jpegFile)}: {ex.Message}");
                        Console.WriteLine();
                    }
                }
            }
            else
            {
                Console.WriteLine("No JPEG files found in the specified path.");
            }
        }
        else
        {
            Console.WriteLine("The specified directory does not exist.");
        }
        Console.ReadLine();
    }

    static byte[] CombineBytes(byte[] array1, byte[] array2)
    {
        byte[] combined = new byte[array1.Length + array2.Length];
        Buffer.BlockCopy(array1, 0, combined, 0, array1.Length);
        Buffer.BlockCopy(array2, 0, combined, array1.Length, array2.Length);
        return combined;
    }
}
