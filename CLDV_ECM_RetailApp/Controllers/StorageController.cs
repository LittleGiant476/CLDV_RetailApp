using Microsoft.AspNetCore.Mvc;

public class StorageController : Controller
{
    private readonly AzureStorageService _service;

    public StorageController(AzureStorageService service)
    {
        _service = service;
    }

    public async Task<IActionResult> Index()
    {
        ViewBag.Customers = await _service.GetCustomers();
        ViewBag.Products = await _service.GetProducts();
        ViewBag.Queues = await _service.GetQueueMessages();
        ViewBag.Blobs = await _service.GetBlobs();
        ViewBag.Logs = await _service.GetLogs();

        return View();
    }

    /*
    public IActionResult Index()
    {
        return View();
    }
    */

    [HttpPost]
    public async Task<IActionResult> AddCustomer(string name, string email, string productName, int quantity)
    {
        try
        {
            await _service.AddCustomer(name, email, productName, quantity);
            ViewBag.Message = "Customer + Order Added!";
        }
        catch (Exception ex)
        {
            ViewBag.Message = ex.Message;
        }

        return View("Index");
    }

    [HttpPost]
    public async Task<IActionResult> AddProduct(string productName, int quantity)
    {
        try
        {
            await _service.AddProduct(productName, quantity);
            ViewBag.Message = "Product Added!";
        }
        catch (Exception ex)
        {
            ViewBag.Message = ex.Message;
        }

        return View("Index");
    }

    [HttpPost]
    public async Task<IActionResult> UploadImage(IFormFile file)
    {
        try
        {
            await _service.UploadImage(file);
            ViewBag.Message = "Image Uploaded!";
        }
        catch (Exception ex)
        {
            ViewBag.Message = ex.Message;
        }

        return View("Index");
    }

    [HttpPost]
    public async Task<IActionResult> SendQueue(string message)
    {
        await _service.SendQueueMessage(message);
        ViewBag.Message = "Queue Message Sent!";
        return View("Index");
    }

    [HttpPost]
    public async Task<IActionResult> SaveLog(string fileName, string content)
    {
        await _service.SaveLog(fileName, content);
        ViewBag.Message = "Log Saved!";
        return View("Index");
    }
}