using System;
using System.IO;
using static System.Net.Mime.MediaTypeNames;
using System.Net.Http.Headers;
using System.Net.Http;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace microsoft.go;

public class Repositories
{
    public List<string> repositoriesNames { get; set; }
}

public class FileDto
{
    public string name { get; set; }

    [JsonConverter(typeof(ByteConverter))] public byte[] file { get; set; }
}

public class ByteConverter : JsonConverter<byte[]>
{
    public override byte[] Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        short[] sByteArray = JsonSerializer.Deserialize<short[]>(ref reader);
        byte[] value = new byte[sByteArray.Length];
        for (int i = 0; i < sByteArray.Length; i++)
        {
            value[i] = (byte) sByteArray[i];
        }

        return value;
    }

    public override void Write(Utf8JsonWriter writer, byte[] value, JsonSerializerOptions options)
    {
        writer.WriteStartArray();

        foreach (var val in value)
        {
            writer.WriteNumberValue(val);
        }

        writer.WriteEndArray();
    }
}

internal class Program
{
    static string directoryPath = Directory.GetCurrentDirectory();
    static string serverAddress = "";
    static string pullCommand = "/files/pull/";
    static string pushCommand = "/files/push";
    static string allCommand = "/files/all";
    static char sep = Path.DirectorySeparatorChar;
    private static bool isProblem = false;

    static async Task Main(string[] args)
    {
        if (args.Length == 0)
        {
            Console.WriteLine("You didn't specify the command");
            return;
        }

        if (args[0] == "set")
        {
            if (args.Length != 2)
            {
                Console.WriteLine("You didn't set the server address");
                return;
            }

            using StreamWriter writer = new StreamWriter(directoryPath + sep + "address.txt", false);
            writer.Write("http://" + args[1]);
            serverAddress = "http://" + args[1];
            Console.WriteLine($"The server address {serverAddress} has written to the file address.txt");
        }
        else if (args[0] == "push")
        {
            if (!File.Exists(directoryPath + sep + "address.txt"))
            {
                Console.WriteLine("You didn't set the server address");
                return;
            }

            serverAddress = File.ReadAllText(directoryPath + sep + "address.txt");
            if (serverAddress == "")
            {
                Console.WriteLine("You didn't set the server address");
                return;
            }

            var directory = new DirectoryInfo(directoryPath);
            await PushFilesAndDirs(directoryPath, directory.Name);
            if (!isProblem)
            {
                Console.WriteLine($"The folder {directory.Name} has been sent to the server");
            }
        }
        else if (args[0] == "all")
        {
            if (!File.Exists(directoryPath + sep + "address.txt"))
            {
                Console.WriteLine("You didn't set the server address");
                return;
            }

            serverAddress = File.ReadAllText(directoryPath + sep + "address.txt");
            if (serverAddress == "")
            {
                Console.WriteLine("You didn't set the server address");
                return;
            }

            await WriteAllDirsToUser();
        }
        else if (args[0] == "pull")
        {
            if (args.Length != 2)
            {
                Console.WriteLine("You didn't set the directory");
                return;
            }

            if (!File.Exists(directoryPath + sep + "address.txt"))
            {
                Console.WriteLine("You didn't set the server address");
                return;
            }

            serverAddress = File.ReadAllText(directoryPath + sep + "address.txt");
            if (serverAddress == "")
            {
                Console.WriteLine("You didn't set the server address");
                return;
            }

            await GetAllFilesAndDirs(args[1]);
            if (!isProblem)
            {
                Console.WriteLine($"The folder {args[1]} recorded in {new DirectoryInfo(directoryPath).Name}");
            }
        }
        else if (args[0] == "info")
        {
            Console.WriteLine("set 'server_address' - the address of the server to send data to");
            Console.WriteLine("push - sends the specified folder to the server");
            Console.WriteLine("pull 'directory_name' - get the specified folder from the server");
            Console.WriteLine("all - get the names of all folders stored on the server");
        }
        else
        {
            Console.WriteLine("Unknown command");
            Console.WriteLine("Try info, it will give information about available commands");
        }
    }

    static async Task PushFilesAndDirs(string directory_path, string dir_path)
    {
        HttpClient httpClient = new HttpClient();
        string[] files = Directory.GetFiles(directory_path);
        if (files.Length != 0)
        {
            var multipartFormContent = new MultipartFormDataContent();
            multipartFormContent.Add(new StringContent(dir_path), name: "directory_name");
            foreach (string file in files)
            {
                FileInfo file_name = new FileInfo(file);
                var fileStreamContent = new StreamContent(File.OpenRead(file));
                multipartFormContent.Add(fileStreamContent, name: "files", fileName: file_name.Name);
            }
            try
            {
                await httpClient.PostAsync(serverAddress + pushCommand, multipartFormContent);
            }
            catch (Exception e)
            {
                Console.WriteLine("Problems with the server address");
                isProblem = true;
                return;
            }
        }

        string[] dirs = Directory.GetDirectories(directory_path);
        foreach (string dir in dirs)
        {
            var directory = new DirectoryInfo(dir);
            await PushFilesAndDirs(dir, dir_path + '\\' + directory.Name);
        }
    }

    static async Task WriteAllDirsToUser()
    {
        HttpClient httpClient = new HttpClient();
        Repositories? repositories;
        try
        {
            repositories = await httpClient.GetFromJsonAsync<Repositories>(serverAddress + allCommand);
        }
        catch (Exception e)
        {
            Console.WriteLine("Problems with the server address");
            isProblem = true;
            return;
        }
        Console.WriteLine("Repositories:");
        foreach (var repository in repositories.repositoriesNames)
        {
            Console.WriteLine(repository);
        }
    }

    static async Task GetAllFilesAndDirs(string directory)
    {
        HttpResponseMessage responseMessage;
        HttpClient httpClient = new HttpClient();
        try
        {
            responseMessage = await httpClient.GetAsync(serverAddress + pullCommand + directory);
            var responseContent =
                JsonSerializer.Deserialize<List<FileDto>>(await responseMessage.Content.ReadAsStringAsync());
            foreach (var content in responseContent)
            {
                CreatOrCheckDirectory(content.name.Split(sep).ToList());
                File.WriteAllBytes(content.name, content.file);
            }
        }
        catch (Exception e)
        {
            Console.WriteLine("Problems with the specified folder or with the server address");
            isProblem = true;
        }
    }

    static void CreatOrCheckDirectory(List<string> pathToFile)
    {
        string currentDir = directoryPath;
        for (int i = 0; i < pathToFile.Count - 1; i++)
        {
            currentDir = currentDir + sep + pathToFile[i];
            if (!Directory.Exists(currentDir))
            {
                Directory.CreateDirectory(currentDir);
            }
        }
    }
}