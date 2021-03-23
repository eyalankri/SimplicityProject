using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Nop.Core;
using Nop.Core.Domain.Customers;
using Nop.Core.Domain.Orders;
using Nop.Core.Domain.Payments;
using Nop.Core.Infrastructure;
using Nop.Services.Catalog;
using Nop.Services.Common;
using Nop.Services.Customers;
using Nop.Services.Orders;
using Nop.Services.Payments;
using Nop.Services.Shipping;
using Nop.Web.Factories;
using Nop.Web.Framework.Controllers;
using Nop.Web.Framework.Mvc.Filters;
using Nop.Web.Models.Customer;
using Nop.Web.Models.Order;

namespace Nop.Web.Controllers
{
    public partial class OrderController : BasePublicController
    {
        #region Fields

        private readonly ICustomerService _customerService;
        private readonly IOrderModelFactory _orderModelFactory;
        private readonly IOrderProcessingService _orderProcessingService;
        private readonly IOrderService _orderService;
        private readonly IPaymentService _paymentService;
        private readonly IPdfService _pdfService;
        private readonly IShipmentService _shipmentService;
        private readonly IWebHelper _webHelper;
        private readonly IWorkContext _workContext;
        private readonly RewardPointsSettings _rewardPointsSettings;
        private readonly INopFileProvider _fileProvider;
        readonly ICustomerModelFactory _customerModelFactory;
        private readonly IProductService _productService;
        private readonly IStoreContext _storeContext;

        #endregion

        #region Ctor

        public OrderController(ICustomerService customerService,
            IOrderModelFactory orderModelFactory,
            IOrderProcessingService orderProcessingService, 
            IOrderService orderService, 
            IPaymentService paymentService, 
            IPdfService pdfService,
            IShipmentService shipmentService, 
            IWebHelper webHelper,
            IWorkContext workContext,
            RewardPointsSettings rewardPointsSettings,
            INopFileProvider fileProvider,
            ICustomerModelFactory customerModelFactory,
            IProductService productService,
            IStoreContext storeContext
            )
        {
            _customerService = customerService;
            _orderModelFactory = orderModelFactory;
            _orderProcessingService = orderProcessingService;
            _orderService = orderService;
            _paymentService = paymentService;
            _pdfService = pdfService;
            _shipmentService = shipmentService;
            _webHelper = webHelper;
            _workContext = workContext;
            _rewardPointsSettings = rewardPointsSettings;
            _fileProvider = fileProvider;
            _customerModelFactory = customerModelFactory;
            _productService = productService;
            _storeContext = storeContext;
        }

        #endregion

        #region Methods

        //My account / Orders
        [HttpsRequirement]
        public virtual IActionResult CustomerOrders()
        {
            if (!_customerService.IsRegistered(_workContext.CurrentCustomer))
                return Challenge();
             

            var model = _orderModelFactory.PrepareCustomerOrderListModel();

            var customerModel = new CustomerInfoModel();
            customerModel = _customerModelFactory.PrepareCustomerInfoModel(customerModel, _workContext.CurrentCustomer, false);
            
            foreach (var order in model.Orders)
            {                
                if (order.PaymentStatus == "שולם" || order.PaymentStatus.ToLower() == "paid")
                {
                     
                    var orderModel = _orderService.GetOrderById(order.Id);
                    var orderDetails = _orderModelFactory.PrepareOrderDetailsModel(orderModel);

                    GenerateQrCode(customerModel, orderDetails);
                }
                
            }

            return View(model);
        }


        private string GenerateQrCode(CustomerInfoModel customer, OrderDetailsModel order)
        {

            var fileName = string.Format("{0}.jpg", order.CustomOrderNumber);
            var path = _fileProvider.GetAbsolutePath("images//QrCodes", fileName);

            var sb  = new StringBuilder();
            sb.Append($"Name: {customer.FirstName} {customer.LastName}%0A");
            sb.Append($"Phone: {customer.Phone}%0A");
            sb.Append($"Order Id: {order.CustomOrderNumber}%0A");
            sb.Append("No of Tickets: " + order.Items.Count + "%0A");

            var i = 1;
            foreach (var item in order.Items)
            {
                sb.Append("Ticket " + i + ". Event: " + item.Sku + "%0A%0A");
                i++;
            }

            sb.Append("SimplicityProject.co.il");


            // %0A - new line
            var url = $"http://chart.apis.google.com/chart?cht=qr&chs=300x300&chl={sb}";
            WebResponse response = default(WebResponse);
            Stream remoteStream = default(Stream);
            StreamReader readStream = default(StreamReader);
            WebRequest request = WebRequest.Create(url);
            response = request.GetResponse();
            remoteStream = response.GetResponseStream();
            readStream = new StreamReader(remoteStream);
            System.Drawing.Image img = System.Drawing.Image.FromStream(remoteStream);

            img.Save(path);
            response.Close();
            remoteStream.Close();
            readStream.Close();


            var sb2 = new StringBuilder();
            sb2.Append($"Name: {customer.FirstName} {customer.LastName}\n");
            sb2.Append($"Phone: {customer.Phone}\n");
            sb2.Append($"Order Id: {order.CustomOrderNumber}\n");
            sb2.Append("No of Tickets: " + order.Items.Count + "\n");

            var i2 = 1;
            foreach (var item in order.Items)
            {
                sb.Append("Ticket " + i + ". Event: " + item.Sku + "\n\n");
                i2++;
            }

            sb.Append("SimplicityProject.co.il");
            return sb.ToString();
        }


         public virtual IActionResult ShowTickets(int orderId)
        {
            if (!_customerService.IsRegistered(_workContext.CurrentCustomer))
                return Challenge();

            var order = _orderService.GetOrderById(orderId);

            
            if (order == null || order.Deleted || _workContext.CurrentCustomer.Id != order.CustomerId)
            {
                return RedirectToRoute("Homepage");
            }


            var orderModel = _orderService.GetOrderById(orderId);
            var orderFactory = _orderModelFactory.PrepareOrderDetailsModel(orderModel);

            var customer = new CustomerInfoModel();
            customer = _customerModelFactory.PrepareCustomerInfoModel(customer, _workContext.CurrentCustomer, false);

            var sb = new StringBuilder();
            sb.Append($"שם: {customer.FirstName} {customer.LastName}<br />");
            sb.Append($"טלפון: {customer.Phone}<br />");
            sb.Append($"מספר הזמנה: {order.CustomOrderNumber}<br />");
            sb.Append("כמות כרטיסים: " + orderFactory.Items.Count + "<br />");

            var i = 1;
            foreach (var item in orderFactory.Items)
            {
                sb.Append("כרטיס " + i + ". אירוע: " + item.Sku + "<br /><br />");
                i++;
            }

          

            ViewBag.Details = sb.ToString();
            ViewBag.OrderId = orderId;

            return View();
        }


        //My account / Orders / Cancel recurring order
        [HttpPost, ActionName("CustomerOrders")]
        [AutoValidateAntiforgeryToken]
        [FormValueRequired(FormValueRequirement.StartsWith, "cancelRecurringPayment")]
        public virtual IActionResult CancelRecurringPayment(IFormCollection form)
        {
            if (!_customerService.IsRegistered(_workContext.CurrentCustomer))
                return Challenge();

            //get recurring payment identifier
            var recurringPaymentId = 0;
            foreach (var formValue in form.Keys)
                if (formValue.StartsWith("cancelRecurringPayment", StringComparison.InvariantCultureIgnoreCase))
                    recurringPaymentId = Convert.ToInt32(formValue.Substring("cancelRecurringPayment".Length));

            var recurringPayment = _orderService.GetRecurringPaymentById(recurringPaymentId);
            if (recurringPayment == null)
            {
                return RedirectToRoute("CustomerOrders");
            }

            if (_orderProcessingService.CanCancelRecurringPayment(_workContext.CurrentCustomer, recurringPayment))
            {
                var errors = _orderProcessingService.CancelRecurringPayment(recurringPayment);

                var model = _orderModelFactory.PrepareCustomerOrderListModel();
                model.RecurringPaymentErrors = errors;

                return View(model);
            }

            return RedirectToRoute("CustomerOrders");
        }

        //My account / Orders / Retry last recurring order
        [HttpPost, ActionName("CustomerOrders")]
        [AutoValidateAntiforgeryToken]
        [FormValueRequired(FormValueRequirement.StartsWith, "retryLastPayment")]
        public virtual IActionResult RetryLastRecurringPayment(IFormCollection form)
        {
            if (!_customerService.IsRegistered(_workContext.CurrentCustomer))
                return Challenge();

            //get recurring payment identifier
            var recurringPaymentId = 0;
            if (!form.Keys.Any(formValue => formValue.StartsWith("retryLastPayment", StringComparison.InvariantCultureIgnoreCase) &&
                int.TryParse(formValue.Substring(formValue.IndexOf('_') + 1), out recurringPaymentId)))
            {
                return RedirectToRoute("CustomerOrders");
            }

            var recurringPayment = _orderService.GetRecurringPaymentById(recurringPaymentId);
            if (recurringPayment == null)
                return RedirectToRoute("CustomerOrders");

            if (!_orderProcessingService.CanRetryLastRecurringPayment(_workContext.CurrentCustomer, recurringPayment))
                return RedirectToRoute("CustomerOrders");

            var errors = _orderProcessingService.ProcessNextRecurringPayment(recurringPayment);
            var model = _orderModelFactory.PrepareCustomerOrderListModel();
            model.RecurringPaymentErrors = errors.ToList();

            return View(model);
        }

        //My account / Reward points
        [HttpsRequirement]
        public virtual IActionResult CustomerRewardPoints(int? pageNumber)
        {
            if (!_customerService.IsRegistered(_workContext.CurrentCustomer))
                return Challenge();

            if (!_rewardPointsSettings.Enabled)
                return RedirectToRoute("CustomerInfo");

            var model = _orderModelFactory.PrepareCustomerRewardPoints(pageNumber);
            return View(model);
        }

        //My account / Order details page
        [HttpsRequirement]
        public virtual IActionResult Details(int orderId)
        {
            var order = _orderService.GetOrderById(orderId);
            if (order == null || order.Deleted || _workContext.CurrentCustomer.Id != order.CustomerId)
                return Challenge();

            var model = _orderModelFactory.PrepareOrderDetailsModel(order);
            return View(model);
        }

        //My account / Order details page / Print
        [HttpsRequirement]
        public virtual IActionResult PrintOrderDetails(int orderId)
        {
            var order = _orderService.GetOrderById(orderId);
            if (order == null || order.Deleted || _workContext.CurrentCustomer.Id != order.CustomerId)
                return Challenge();

            var model = _orderModelFactory.PrepareOrderDetailsModel(order);
            model.PrintMode = true;

            return View("Details", model);
        }

        //My account / Order details page / PDF invoice
        public virtual IActionResult GetPdfInvoice(int orderId)
        {
            var order = _orderService.GetOrderById(orderId);
            if (order == null || order.Deleted || _workContext.CurrentCustomer.Id != order.CustomerId)
                return Challenge();

            var orders = new List<Order>();
            orders.Add(order);
            byte[] bytes;
            using (var stream = new MemoryStream())
            {
                _pdfService.PrintOrdersToPdf(stream, orders, _workContext.WorkingLanguage.Id);
                bytes = stream.ToArray();
            }
            return File(bytes, MimeTypes.ApplicationPdf, $"order_{order.Id}.pdf");
        }

        //My account / Order details page / re-order
        public virtual IActionResult ReOrder(int orderId)
        {
            var order = _orderService.GetOrderById(orderId);
            if (order == null || order.Deleted || _workContext.CurrentCustomer.Id != order.CustomerId)
                return Challenge();

            _orderProcessingService.ReOrder(order);
            return RedirectToRoute("ShoppingCart");
        }

        //My account / Order details page / Complete payment
        [HttpPost, ActionName("Details")]
        [AutoValidateAntiforgeryToken]
        [FormValueRequired("repost-payment")]
        public virtual IActionResult RePostPayment(int orderId)
        {
            var order = _orderService.GetOrderById(orderId);
            if (order == null || order.Deleted || _workContext.CurrentCustomer.Id != order.CustomerId)
                return Challenge();

            if (!_paymentService.CanRePostProcessPayment(order))
                return RedirectToRoute("OrderDetails", new { orderId = orderId });

            var postProcessPaymentRequest = new PostProcessPaymentRequest
            {
                Order = order
            };
            _paymentService.PostProcessPayment(postProcessPaymentRequest);

            if (_webHelper.IsRequestBeingRedirected || _webHelper.IsPostBeingDone)
            {
                //redirection or POST has been done in PostProcessPayment
                return Content("Redirected");
            }

            //if no redirection has been done (to a third-party payment page)
            //theoretically it's not possible
            return RedirectToRoute("OrderDetails", new { orderId = orderId });
        }

        //My account / Order details page / Shipment details page
        [HttpsRequirement]
        public virtual IActionResult ShipmentDetails(int shipmentId)
        {
            var shipment = _shipmentService.GetShipmentById(shipmentId);
            if (shipment == null)
                return Challenge();

            var order = _orderService.GetOrderById(shipment.OrderId);
            
            if (order == null || order.Deleted || _workContext.CurrentCustomer.Id != order.CustomerId)
                return Challenge();

            var model = _orderModelFactory.PrepareShipmentDetailsModel(shipment);
            return View(model);
        }
        
        #endregion
    }
}