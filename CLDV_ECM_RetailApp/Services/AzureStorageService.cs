using Azure;
using Azure.Data.Tables;
using Azure.Storage.Blobs;
using Azure.Storage.Queues;
using Azure.Storage.Files.Shares;
using Microsoft.Extensions.Configuration;

public class AzureStorageService
{
    private readonly string _connectionString;

    public AzureStorageService(IConfiguration config)
    {
        _connectionString = config.GetValue<string>("AzureStorage");

        if (string.IsNullOrEmpty(_connectionString))
        {
            throw new Exception("Azure Storage connection string is missing!");
        }
    }

    // TABLE STORAGE
    public async Task AddCustomer(string name, string email, string productName, int quantity)
    {
        // 1. Save Customer
        var customerTable = new TableClient(_connectionString, "Customers");
        await customerTable.CreateIfNotExistsAsync();

        var customerEntity = new TableEntity("Customer", Guid.NewGuid().ToString())
    {
        { "Name", name },
        { "Email", email }
    };

        await customerTable.AddEntityAsync(customerEntity);

        string message = $"{name} added to database";

        // 2. IF product exists → Save to Product table with FK
        if (!string.IsNullOrEmpty(productName) && quantity > 0)
        {
            var productTable = new TableClient(_connectionString, "Products");
            await productTable.CreateIfNotExistsAsync();

            var productEntity = new TableEntity("Product", Guid.NewGuid().ToString())
        {
            { "ProductName", productName },
            { "Quantity", quantity },
            { "CustomerEmail", email } // 🔥 Foreign Key -> Yes I found a way to add emojis 
        };

            await productTable.AddEntityAsync(productEntity);

            message = $"{name} added to database and ordered {quantity} x {productName}";
        }

        // 3. Queue
        await SendQueueMessage(message);

        // 4. Logs (clean file name)
        string safeFileName = message.Replace(" ", "_") + ".txt";
        await SaveLog(safeFileName, message);
    }


    public async Task<List<TableEntity>> GetCustomers()
    {
        var table = new TableClient(_connectionString, "Customers");
        var results = new List<TableEntity>();

        await foreach (var entity in table.QueryAsync<TableEntity>())
        {
            results.Add(entity);
        }

        return results;
    }


    public async Task AddProduct(string productName, int quantity)
    {
        var tableClient = new TableClient(_connectionString, "Products");
        await tableClient.CreateIfNotExistsAsync();

        var entity = new TableEntity("Product", Guid.NewGuid().ToString())
    {
        { "ProductName", productName },
        { "Quantity", quantity },
        { "CustomerEmail", "N/A" } // no FK
    };

        await tableClient.AddEntityAsync(entity);

        string message = $"Product added: {productName} (Quantity: {quantity})";

        await SendQueueMessage(message);

        string safeFileName = $"log_{DateTime.UtcNow:yyyyMMddHHmmss}.txt";
        await SaveLog(safeFileName, message);
    }

    public async Task<List<TableEntity>> GetProducts()
    {
        var table = new TableClient(_connectionString, "Products");
        var results = new List<TableEntity>();

        await foreach (var entity in table.QueryAsync<TableEntity>())
        {
            results.Add(entity);
        }

        return results;
    }


    // BLOB STORAGE

    public async Task UploadImage(IFormFile file)
    {
        if (file == null || file.Length == 0)
            throw new Exception("No file selected!");

        try
        {
            var blobServiceClient = new BlobServiceClient(_connectionString);
            var container = blobServiceClient.GetBlobContainerClient("images");

            await container.CreateIfNotExistsAsync();

            var blob = container.GetBlobClient(file.FileName);

            using var stream = file.OpenReadStream();
            await blob.UploadAsync(stream, true);
        }
        catch (Exception ex)
        {
            throw new Exception("Blob upload failed: " + ex.Message);
        }
    }


    public async Task<List<string>> GetBlobs()
    {
        var blobService = new BlobServiceClient(_connectionString);
        var container = blobService.GetBlobContainerClient("images");

        var blobs = new List<string>();

        await foreach (var blob in container.GetBlobsAsync())
        {
            var blobClient = container.GetBlobClient(blob.Name);
            blobs.Add(blobClient.Uri.ToString()); // 🔥 FULL URL
        }

        return blobs;
    }

    /*
    public async Task UploadImage(IFormFile file)
    {
        var blobServiceClient = new BlobServiceClient(_connectionString);
        var container = blobServiceClient.GetBlobContainerClient("images");

        await container.CreateIfNotExistsAsync();

        var blob = container.GetBlobClient(file.FileName);

        using var stream = file.OpenReadStream();
        await blob.UploadAsync(stream, true);
    }

    */

    // QUEUE STORAGE
    public async Task SendQueueMessage(string message)
    {
        var queueClient = new QueueClient(_connectionString, "orders");
        await queueClient.CreateIfNotExistsAsync();

        await queueClient.SendMessageAsync(message);
    }


    public async Task<List<string>> GetQueueMessages()
    {
        var queue = new QueueClient(_connectionString, "orders");
        var messages = new List<string>();

        if (await queue.ExistsAsync())
        {
            var retrieved = await queue.ReceiveMessagesAsync(10);

            foreach (var msg in retrieved.Value)
            {
                messages.Add(msg.MessageText);
            }
        }

        return messages;
    }

    // FILE STORAGE
    public async Task SaveLog(string fileName, string content)
    {
        var shareClient = new ShareClient(_connectionString, "logs");
        await shareClient.CreateIfNotExistsAsync();

        var dir = shareClient.GetRootDirectoryClient();
        var file = dir.GetFileClient(fileName);

        var bytes = System.Text.Encoding.UTF8.GetBytes(content);
        using var stream = new MemoryStream(bytes);

        await file.CreateAsync(stream.Length);
        await file.UploadAsync(stream);
    }

    public async Task<List<string>> GetLogs()
    {
        var share = new ShareClient(_connectionString, "logs");
        var dir = share.GetRootDirectoryClient();

        var files = new List<string>();

        await foreach (var item in dir.GetFilesAndDirectoriesAsync())
        {
            if (!item.IsDirectory)
                files.Add(item.Name);
        }

        return files;
    }
}