using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Gemstone.Configuration;
using Gemstone.Data;
using Gemstone.Data.Model;
using Gemstone.Reflection;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using openXDA.Model;
using PQDigest.Models;
using PQDigest.Security;
using SystemCenter.Model;

namespace PQDigest.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class SettingController : ControllerBase
    {
        private const string DefaultLogoPath = "Image/GPA_Horizontal.png";
        private readonly IWebHostEnvironment m_environment;

        public SettingController(IWebHostEnvironment environment)
        {
            m_environment = environment;
        }

        [Route("{name}")]
        public ActionResult Get(string name)
        {
            using (AdoDataConnection connection = new AdoDataConnection(Settings.Default))
            {
                return Ok(new TableOperations<Setting>(connection).QueryRecordWhere("Name = {0}", name));
            }
        }

        [HttpGet, Route("Logo")]
        public IActionResult GetLogo()
        {

            using (AdoDataConnection connection = new AdoDataConnection(Settings.Default))
            {
                Customer? customer = HttpContext.User.GetCustomer(connection);
                string webRoot = m_environment.WebRootPath;

                if (customer is null)
                    return Ok(ConvertImageToBase64(Path.Combine(webRoot, DefaultLogoPath)));

                string[] files = Directory.GetFiles(webRoot, Path.Combine("Image", "CompanyLogos", customer.Name + ".*"));
                if (files.Length > 0)
                    return Ok(ConvertImageToBase64(files[0]));

                return Ok(ConvertImageToBase64(Path.Combine(webRoot, DefaultLogoPath)));
            }
        }

        [HttpGet, Route("CustomerInfo")]
        public IActionResult GetCustomerInfo()
        {
            using (AdoDataConnection connection = new AdoDataConnection(Settings.Default))
            {
                Customer? customer = HttpContext.User.GetCustomer(connection);
                string customerKey = HttpContext.User.GetCustomer();
                string[] errors = Array.Empty<string>();

                if (customer is null)
                {
                    customer = new Customer
                    {
                        CustomerKey = customerKey
                    };

                    errors = new[] { $"Unable to find company with CustomerKey: {customerKey}. Contact your System Administrator to resolve this." };
                }

                return Ok(new { Customer = customer, Error = errors });
            }
        }

        private string ConvertImageToBase64(string filePath)
        {
            byte[] imageBytes = System.IO.File.ReadAllBytes(filePath);
            return Convert.ToBase64String(imageBytes);
        }
    }
}
