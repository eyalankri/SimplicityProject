﻿using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Nop.Core;
using Nop.Core.Domain.Common;
using Nop.Core.Domain.Customers;
using Nop.Core.Domain.Orders;
using Nop.Core.Domain.Payments;
using Nop.Core.Domain.Shipping;
using Nop.Core.Http.Extensions;
using Nop.Core.Infrastructure;
using Nop.Data;
using Nop.Services.Catalog;
using Nop.Services.Common;
using Nop.Services.Customers;
using Nop.Services.Directory;
using Nop.Services.Localization;
using Nop.Services.Logging;
using Nop.Services.Orders;
using Nop.Services.Payments;
using Nop.Services.Shipping;
using Nop.Web.Extensions;
using Nop.Web.Factories;
using Nop.Web.Framework.Controllers;
using Nop.Web.Framework.Mvc.Filters;
using Nop.Web.Models.Checkout;
using Nop.Web.Models.Customer;
using Nop.Web.Models.Order;

namespace Nop.Web.Controllers
{
    [HttpsRequirement]
    [AutoValidateAntiforgeryToken]
    public partial class CheckoutController : BasePublicController
    {
        #region Fields

        private readonly AddressSettings _addressSettings;
        private readonly CustomerSettings _customerSettings;
        private readonly IAddressAttributeParser _addressAttributeParser;
        private readonly IAddressService _addressService;
        private readonly ICheckoutModelFactory _checkoutModelFactory;
        private readonly ICountryService _countryService;
        private readonly ICustomerService _customerService;
        private readonly IGenericAttributeService _genericAttributeService;
        private readonly ILocalizationService _localizationService;
        private readonly ILogger _logger;
        private readonly IOrderProcessingService _orderProcessingService;
        private readonly IOrderService _orderService;
        private readonly IPaymentPluginManager _paymentPluginManager;
        private readonly IPaymentService _paymentService;
        private readonly IProductService _productService;
        private readonly IShippingService _shippingService;
        private readonly IShoppingCartService _shoppingCartService;
        private readonly IStoreContext _storeContext;
        private readonly IWebHelper _webHelper;
        private readonly IWorkContext _workContext;
        private readonly OrderSettings _orderSettings;
        private readonly PaymentSettings _paymentSettings;
        private readonly RewardPointsSettings _rewardPointsSettings;
        private readonly ShippingSettings _shippingSettings;
        private readonly IOrderModelFactory _orderModelFactory;
        private readonly Areas.Admin.Factories.IProductModelFactory _productModelFactory;
        private readonly ICustomerModelFactory _customerModelFactory;
        private readonly INopFileProvider _fileProvider;

        #endregion

        #region Ctor

        public CheckoutController(AddressSettings addressSettings,
            CustomerSettings customerSettings,
            IAddressAttributeParser addressAttributeParser,
            IAddressService addressService,
            ICheckoutModelFactory checkoutModelFactory,
            ICountryService countryService,
            ICustomerService customerService,
            IGenericAttributeService genericAttributeService,
            ILocalizationService localizationService,
            ILogger logger,
            IOrderProcessingService orderProcessingService,
            IOrderService orderService,
            IPaymentPluginManager paymentPluginManager,
            IPaymentService paymentService,
            IProductService productService,
            IShippingService shippingService,
            IShoppingCartService shoppingCartService,
            IStoreContext storeContext,
            IWebHelper webHelper,
            IWorkContext workContext,
            OrderSettings orderSettings,
            PaymentSettings paymentSettings,
            RewardPointsSettings rewardPointsSettings,
            ShippingSettings shippingSettings,
            IOrderModelFactory orderModelFactory,
            Areas.Admin.Factories.IProductModelFactory productModelFactory,
            ICustomerModelFactory customerModelFactory,
            INopFileProvider fileProvider)
        {
            _addressSettings = addressSettings;
            _customerSettings = customerSettings;
            _addressAttributeParser = addressAttributeParser;
            _addressService = addressService;
            _checkoutModelFactory = checkoutModelFactory;
            _countryService = countryService;
            _customerService = customerService;
            _genericAttributeService = genericAttributeService;
            _localizationService = localizationService;
            _logger = logger;
            _orderProcessingService = orderProcessingService;
            _orderService = orderService;
            _paymentPluginManager = paymentPluginManager;
            _paymentService = paymentService;
            _productService = productService;
            _shippingService = shippingService;
            _shoppingCartService = shoppingCartService;
            _storeContext = storeContext;
            _webHelper = webHelper;
            _workContext = workContext;
            _orderSettings = orderSettings;
            _paymentSettings = paymentSettings;
            _rewardPointsSettings = rewardPointsSettings;
            _shippingSettings = shippingSettings;
            _orderModelFactory = orderModelFactory;
            _productModelFactory = productModelFactory;
            _customerModelFactory = customerModelFactory;
            _fileProvider = fileProvider;
        }

        #endregion

        #region Utilities

        protected virtual bool IsMinimumOrderPlacementIntervalValid(Customer customer)
        {
            //prevent 2 orders being placed within an X seconds time frame
            if (_orderSettings.MinimumOrderPlacementInterval == 0)
                return true;

            var lastOrder = _orderService.SearchOrders(storeId: _storeContext.CurrentStore.Id,
                customerId: _workContext.CurrentCustomer.Id, pageSize: 1)
                .FirstOrDefault();
            if (lastOrder == null)
                return true;

            var interval = DateTime.UtcNow - lastOrder.CreatedOnUtc;
            return interval.TotalSeconds > _orderSettings.MinimumOrderPlacementInterval;
        }

        /// <summary>
        /// Parses the value indicating whether the "pickup in store" is allowed
        /// </summary>
        /// <param name="form">The form</param>
        /// <returns>The value indicating whether the "pickup in store" is allowed</returns>
        protected virtual bool ParsePickupInStore(IFormCollection form)
        {
            var pickupInStore = false;

            var pickupInStoreParameter = form["PickupInStore"].FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(pickupInStoreParameter))
                bool.TryParse(pickupInStoreParameter, out pickupInStore);

            return pickupInStore;
        }

        /// <summary>
        /// Parses the pickup option
        /// </summary>
        /// <param name="form">The form</param>
        /// <returns>The pickup option</returns>
        protected virtual PickupPoint ParsePickupOption(IFormCollection form)
        {
            var pickupPoint = form["pickup-points-id"].ToString().Split(new[] { "___" }, StringSplitOptions.None);
            var pickupPoints = _shippingService.GetPickupPoints(_workContext.CurrentCustomer.BillingAddressId ?? 0,
                _workContext.CurrentCustomer, pickupPoint[1], _storeContext.CurrentStore.Id).PickupPoints.ToList();
            var selectedPoint = pickupPoints.FirstOrDefault(x => x.Id.Equals(pickupPoint[0]));
            if (selectedPoint == null)
                throw new Exception("Pickup point is not allowed");

            return selectedPoint;
        }

        /// <summary>
        /// Saves the pickup option
        /// </summary>
        /// <param name="pickupPoint">The pickup option</param>
        protected virtual void SavePickupOption(PickupPoint pickupPoint)
        {
            var pickUpInStoreShippingOption = new ShippingOption
            {
                Name = string.Format(_localizationService.GetResource("Checkout.PickupPoints.Name"), pickupPoint.Name),
                Rate = pickupPoint.PickupFee,
                Description = pickupPoint.Description,
                ShippingRateComputationMethodSystemName = pickupPoint.ProviderSystemName,
                IsPickupInStore = true
            };
            _genericAttributeService.SaveAttribute(_workContext.CurrentCustomer, NopCustomerDefaults.SelectedShippingOptionAttribute, pickUpInStoreShippingOption, _storeContext.CurrentStore.Id);
            _genericAttributeService.SaveAttribute(_workContext.CurrentCustomer, NopCustomerDefaults.SelectedPickupPointAttribute, pickupPoint, _storeContext.CurrentStore.Id);
        }

        #endregion

        #region Methods (common)

        public virtual IActionResult Index()
        {
            //validation
            if (_orderSettings.CheckoutDisabled)
                return RedirectToRoute("ShoppingCart");

            var cart = _shoppingCartService.GetShoppingCart(_workContext.CurrentCustomer, ShoppingCartType.ShoppingCart, _storeContext.CurrentStore.Id);

            if (!cart.Any())
                return RedirectToRoute("ShoppingCart");

            var cartProductIds = cart.Select(ci => ci.ProductId).ToArray();
            var downloadableProductsRequireRegistration =
                _customerSettings.RequireRegistrationForDownloadableProducts && _productService.HasAnyDownloadableProduct(cartProductIds);

            if (_customerService.IsGuest(_workContext.CurrentCustomer) && (!_orderSettings.AnonymousCheckoutAllowed || downloadableProductsRequireRegistration))
                return Challenge();

            //if we have only "button" payment methods available (displayed onthe shopping cart page, not during checkout),
            //then we should allow standard checkout
            //all payment methods (do not filter by country here as it could be not specified yet)
            var paymentMethods = _paymentPluginManager
                .LoadActivePlugins(_workContext.CurrentCustomer, _storeContext.CurrentStore.Id)
                .Where(pm => !pm.HidePaymentMethod(cart)).ToList();
            //payment methods displayed during checkout (not with "Button" type)
            var nonButtonPaymentMethods = paymentMethods
                .Where(pm => pm.PaymentMethodType != PaymentMethodType.Button)
                .ToList();
            //"button" payment methods(*displayed on the shopping cart page)
            var buttonPaymentMethods = paymentMethods
                .Where(pm => pm.PaymentMethodType == PaymentMethodType.Button)
                .ToList();
            if (!nonButtonPaymentMethods.Any() && buttonPaymentMethods.Any())
                return RedirectToRoute("ShoppingCart");

            //reset checkout data
            _customerService.ResetCheckoutData(_workContext.CurrentCustomer, _storeContext.CurrentStore.Id);

            //validation (cart)
            var checkoutAttributesXml = _genericAttributeService.GetAttribute<string>(_workContext.CurrentCustomer,
                NopCustomerDefaults.CheckoutAttributes, _storeContext.CurrentStore.Id);
            var scWarnings = _shoppingCartService.GetShoppingCartWarnings(cart, checkoutAttributesXml, true);
            if (scWarnings.Any())
                return RedirectToRoute("ShoppingCart");
            //validation (each shopping cart item)
            foreach (var sci in cart)
            {
                var product = _productService.GetProductById(sci.ProductId);

                var sciWarnings = _shoppingCartService.GetShoppingCartItemWarnings(_workContext.CurrentCustomer,
                    sci.ShoppingCartType,
                    product,
                    sci.StoreId,
                    sci.AttributesXml,
                    sci.CustomerEnteredPrice,
                    sci.RentalStartDateUtc,
                    sci.RentalEndDateUtc,
                    sci.Quantity,
                    false,
                    sci.Id);
                if (sciWarnings.Any())
                    return RedirectToRoute("ShoppingCart");
            }

            if (_orderSettings.OnePageCheckoutEnabled)
                return RedirectToRoute("CheckoutOnePage");

            return RedirectToRoute("CheckoutBillingAddress");
        }

        [IgnoreAntiforgeryToken]
        public virtual IActionResult Completed(int? orderId)
        {
            //validation
            if (_customerService.IsGuest(_workContext.CurrentCustomer) && !_orderSettings.AnonymousCheckoutAllowed)
                return Challenge();

            Order order = null;
            var orderDetailsModel = new OrderDetailsModel();



            if (orderId.HasValue)
            {
                //load order by identifier (if provided)
                order = _orderService.GetOrderById(orderId.Value);
                orderDetailsModel = _orderModelFactory.PrepareOrderDetailsModel(order);

                //var customer = _customerService.GetCustomerById(_workContext.CurrentCustomer.Id);

                // Remove from the customer all the roles that attached to the tickets he order
                // I am doing this to prevent the user to buy more tickes (I control the ammount from the product configuration)


                // get all the CustomerRoles attached to the products in the order
                foreach (var orderProd in orderDetailsModel.Items)
                {
                    var product = _productService.GetProductById(orderProd.ProductId);
                    var prodModel = _productModelFactory.PrepareProductModel(null, product);

                    foreach (var prodRole in prodModel.AvailableCustomerRoles)
                    {
                        if (prodRole.Selected && int.Parse(prodRole.Value) > 5)  // Dont remove by mistake roles 1-5 (system)
                        {
                            // remove this product role from the user
                            RemoveCustomerRold(order.CustomerId, int.Parse(prodRole.Value));
                        }
                    }
                }

                // I disabled QR code save here - it's moved to order/history (generated live)
                //var customerModel = new CustomerInfoModel();
                //customerModel = _customerModelFactory.PrepareCustomerInfoModel(customerModel, _workContext.CurrentCustomer, false);
                //GenerateQrCode(customerModel, orderDetailsModel.CustomOrderNumber);

            }
            if (order == null)
            {
                order = _orderService.SearchOrders(storeId: _storeContext.CurrentStore.Id,
                customerId: _workContext.CurrentCustomer.Id, pageSize: 1)
                    .FirstOrDefault();
            }
            if (order == null || order.Deleted || _workContext.CurrentCustomer.Id != order.CustomerId)
            {
                return RedirectToRoute("Homepage");
            }

            //disable "order completed" page?
            if (_orderSettings.DisableOrderCompletedPage)
            {
                return RedirectToRoute("OrderDetails", new { orderId = order.Id });
            }

            //model
            var model = _checkoutModelFactory.PrepareCheckoutCompletedModel(order);
            return View(model);
        }


        private void RemoveCustomerRold(int customerId, int roleId)
        {

            var connectionString = DataSettingsManager.LoadSettings().ConnectionString;

            using (var conn = new SqlConnection(connectionString))
            {
                var cmd = new SqlCommand("[CustomerRole_Remove]", conn) { CommandType = CommandType.StoredProcedure };
                cmd.Parameters.AddWithValue("@CustomerId", customerId);
                cmd.Parameters.AddWithValue("@RoleId", roleId);
                conn.Open();
                cmd.ExecuteNonQuery();
                conn.Close();
            }
        }

        private void GenerateQrCode(CustomerInfoModel customer, string orderId)
        {

            var fileName = string.Format("{0}.jpg",orderId);
            var path = _fileProvider.GetAbsolutePath("images//QrCodes", fileName);

            //_fileProvider.CreateDirectory(thumbsDirectoryPath);
            //var thumbFilePath = _fileProvider.Combine(thumbsDirectoryPath, thumbFileName);

            // %0A - new line
            var url = string.Format("http://chart.apis.google.com/chart?cht=qr&chs=300x300&chl={0}-{1}%0APhone:{2}%0AOrder:{3}%0ASimplicityProject.co.il", customer.FirstName, customer.LastName, customer.Phone, orderId);
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


        }
        #endregion

        #region Methods (multistep checkout)

        public virtual IActionResult BillingAddress(IFormCollection form)
        {
            //validation
            if (_orderSettings.CheckoutDisabled)
                return RedirectToRoute("ShoppingCart");

            var cart = _shoppingCartService.GetShoppingCart(_workContext.CurrentCustomer, ShoppingCartType.ShoppingCart, _storeContext.CurrentStore.Id);

            if (!cart.Any())
                return RedirectToRoute("ShoppingCart");

            if (_orderSettings.OnePageCheckoutEnabled)
                return RedirectToRoute("CheckoutOnePage");

            if (_customerService.IsGuest(_workContext.CurrentCustomer) && !_orderSettings.AnonymousCheckoutAllowed)
                return Challenge();

            //model
            var model = _checkoutModelFactory.PrepareBillingAddressModel(cart, prePopulateNewAddressWithCustomerFields: true);

            //check whether "billing address" step is enabled
            if (_orderSettings.DisableBillingAddressCheckoutStep && model.ExistingAddresses.Any())
            {
                if (model.ExistingAddresses.Any())
                {
                    //choose the first one
                    return SelectBillingAddress(model.ExistingAddresses.First().Id);
                }

                TryValidateModel(model);
                TryValidateModel(model.BillingNewAddress);
                return NewBillingAddress(model, form);
            }

            return View(model);
        }

        public virtual IActionResult SelectBillingAddress(int addressId, bool shipToSameAddress = false)
        {
            //validation
            if (_orderSettings.CheckoutDisabled)
                return RedirectToRoute("ShoppingCart");

            var address = _customerService.GetCustomerAddress(_workContext.CurrentCustomer.Id, addressId);

            if (address == null)
                return RedirectToRoute("CheckoutBillingAddress");

            _workContext.CurrentCustomer.BillingAddressId = address.Id;
            _customerService.UpdateCustomer(_workContext.CurrentCustomer);

            var cart = _shoppingCartService.GetShoppingCart(_workContext.CurrentCustomer, ShoppingCartType.ShoppingCart, _storeContext.CurrentStore.Id);

            //ship to the same address?
            //by default Shipping is available if the country is not specified
            var shippingAllowed = _addressSettings.CountryEnabled ? _countryService.GetCountryByAddress(address)?.AllowsShipping ?? false : true;
            if (_shippingSettings.ShipToSameAddress && shipToSameAddress && _shoppingCartService.ShoppingCartRequiresShipping(cart) && shippingAllowed)
            {
                _workContext.CurrentCustomer.ShippingAddressId = _workContext.CurrentCustomer.BillingAddressId;
                _customerService.UpdateCustomer(_workContext.CurrentCustomer);
                //reset selected shipping method (in case if "pick up in store" was selected)
                _genericAttributeService.SaveAttribute<ShippingOption>(_workContext.CurrentCustomer, NopCustomerDefaults.SelectedShippingOptionAttribute, null, _storeContext.CurrentStore.Id);
                _genericAttributeService.SaveAttribute<PickupPoint>(_workContext.CurrentCustomer, NopCustomerDefaults.SelectedPickupPointAttribute, null, _storeContext.CurrentStore.Id);
                //limitation - "Ship to the same address" doesn't properly work in "pick up in store only" case (when no shipping plugins are available) 
                return RedirectToRoute("CheckoutShippingMethod");
            }

            return RedirectToRoute("CheckoutShippingAddress");
        }

        [HttpPost, ActionName("BillingAddress")]
        [FormValueRequired("nextstep")]
        public virtual IActionResult NewBillingAddress(CheckoutBillingAddressModel model, IFormCollection form)
        {
            //validation
            if (_orderSettings.CheckoutDisabled)
                return RedirectToRoute("ShoppingCart");

            var cart = _shoppingCartService.GetShoppingCart(_workContext.CurrentCustomer, ShoppingCartType.ShoppingCart, _storeContext.CurrentStore.Id);

            if (!cart.Any())
                return RedirectToRoute("ShoppingCart");

            if (_orderSettings.OnePageCheckoutEnabled)
                return RedirectToRoute("CheckoutOnePage");

            if (_customerService.IsGuest(_workContext.CurrentCustomer) && !_orderSettings.AnonymousCheckoutAllowed)
                return Challenge();

            //custom address attributes
            var customAttributes = _addressAttributeParser.ParseCustomAddressAttributes(form);
            var customAttributeWarnings = _addressAttributeParser.GetAttributeWarnings(customAttributes);
            foreach (var error in customAttributeWarnings)
            {
                ModelState.AddModelError("", error);
            }

            var newAddress = model.BillingNewAddress;

            if (ModelState.IsValid)
            {
                //try to find an address with the same values (don't duplicate records)
                var address = _addressService.FindAddress(_customerService.GetAddressesByCustomerId(_workContext.CurrentCustomer.Id).ToList(),
                    newAddress.FirstName, newAddress.LastName, newAddress.PhoneNumber,
                    newAddress.Email, newAddress.FaxNumber, newAddress.Company,
                    newAddress.Address1, newAddress.Address2, newAddress.City,
                    newAddress.County, newAddress.StateProvinceId, newAddress.ZipPostalCode,
                    newAddress.CountryId, customAttributes);

                if (address == null)
                {
                    //address is not found. let's create a new one
                    address = newAddress.ToEntity();
                    address.CustomAttributes = customAttributes;
                    address.CreatedOnUtc = DateTime.UtcNow;

                    //some validation
                    if (address.CountryId == 0)
                        address.CountryId = null;
                    if (address.StateProvinceId == 0)
                        address.StateProvinceId = null;

                    _addressService.InsertAddress(address);

                    _customerService.InsertCustomerAddress(_workContext.CurrentCustomer, address);
                }

                _workContext.CurrentCustomer.BillingAddressId = address.Id;

                _customerService.UpdateCustomer(_workContext.CurrentCustomer);

                //ship to the same address?
                if (_shippingSettings.ShipToSameAddress && model.ShipToSameAddress && _shoppingCartService.ShoppingCartRequiresShipping(cart))
                {
                    _workContext.CurrentCustomer.ShippingAddressId = _workContext.CurrentCustomer.BillingAddressId;
                    _customerService.UpdateCustomer(_workContext.CurrentCustomer);

                    //reset selected shipping method (in case if "pick up in store" was selected)
                    _genericAttributeService.SaveAttribute<ShippingOption>(_workContext.CurrentCustomer, NopCustomerDefaults.SelectedShippingOptionAttribute, null, _storeContext.CurrentStore.Id);
                    _genericAttributeService.SaveAttribute<PickupPoint>(_workContext.CurrentCustomer, NopCustomerDefaults.SelectedPickupPointAttribute, null, _storeContext.CurrentStore.Id);

                    //limitation - "Ship to the same address" doesn't properly work in "pick up in store only" case (when no shipping plugins are available) 
                    return RedirectToRoute("CheckoutShippingMethod");
                }

                return RedirectToRoute("CheckoutShippingAddress");
            }

            //If we got this far, something failed, redisplay form
            model = _checkoutModelFactory.PrepareBillingAddressModel(cart,
                selectedCountryId: newAddress.CountryId,
                overrideAttributesXml: customAttributes);
            return View(model);
        }

        public virtual IActionResult ShippingAddress()
        {
            //validation
            if (_orderSettings.CheckoutDisabled)
                return RedirectToRoute("ShoppingCart");

            var cart = _shoppingCartService.GetShoppingCart(_workContext.CurrentCustomer, ShoppingCartType.ShoppingCart, _storeContext.CurrentStore.Id);

            if (!cart.Any())
                return RedirectToRoute("ShoppingCart");

            if (_orderSettings.OnePageCheckoutEnabled)
                return RedirectToRoute("CheckoutOnePage");

            if (_customerService.IsGuest(_workContext.CurrentCustomer) && !_orderSettings.AnonymousCheckoutAllowed)
                return Challenge();

            if (!_shoppingCartService.ShoppingCartRequiresShipping(cart))
                return RedirectToRoute("CheckoutShippingMethod");

            //model
            var model = _checkoutModelFactory.PrepareShippingAddressModel(cart, prePopulateNewAddressWithCustomerFields: true);
            return View(model);
        }

        public virtual IActionResult SelectShippingAddress(int addressId)
        {
            //validation
            if (_orderSettings.CheckoutDisabled)
                return RedirectToRoute("ShoppingCart");

            var address = _customerService.GetCustomerAddress(_workContext.CurrentCustomer.Id, addressId);

            if (address == null)
                return RedirectToRoute("CheckoutShippingAddress");

            _workContext.CurrentCustomer.ShippingAddressId = address.Id;
            _customerService.UpdateCustomer(_workContext.CurrentCustomer);

            if (_shippingSettings.AllowPickupInStore)
            {
                //set value indicating that "pick up in store" option has not been chosen
                _genericAttributeService.SaveAttribute<PickupPoint>(_workContext.CurrentCustomer, NopCustomerDefaults.SelectedPickupPointAttribute, null, _storeContext.CurrentStore.Id);
            }

            return RedirectToRoute("CheckoutShippingMethod");
        }

        [HttpPost, ActionName("ShippingAddress")]
        [FormValueRequired("nextstep")]
        public virtual IActionResult NewShippingAddress(CheckoutShippingAddressModel model, IFormCollection form)
        {
            //validation
            if (_orderSettings.CheckoutDisabled)
                return RedirectToRoute("ShoppingCart");

            var cart = _shoppingCartService.GetShoppingCart(_workContext.CurrentCustomer, ShoppingCartType.ShoppingCart, _storeContext.CurrentStore.Id);

            if (!cart.Any())
                return RedirectToRoute("ShoppingCart");

            if (_orderSettings.OnePageCheckoutEnabled)
                return RedirectToRoute("CheckoutOnePage");

            if (_customerService.IsGuest(_workContext.CurrentCustomer) && !_orderSettings.AnonymousCheckoutAllowed)
                return Challenge();

            if (!_shoppingCartService.ShoppingCartRequiresShipping(cart))
                return RedirectToRoute("CheckoutShippingMethod");

            //pickup point
            if (_shippingSettings.AllowPickupInStore && !_orderSettings.DisplayPickupInStoreOnShippingMethodPage)
            {
                var pickupInStore = ParsePickupInStore(form);
                if (pickupInStore)
                {
                    var pickupOption = ParsePickupOption(form);
                    SavePickupOption(pickupOption);

                    return RedirectToRoute("CheckoutPaymentMethod");
                }

                //set value indicating that "pick up in store" option has not been chosen
                _genericAttributeService.SaveAttribute<PickupPoint>(_workContext.CurrentCustomer, NopCustomerDefaults.SelectedPickupPointAttribute, null, _storeContext.CurrentStore.Id);
            }

            //custom address attributes
            var customAttributes = _addressAttributeParser.ParseCustomAddressAttributes(form);
            var customAttributeWarnings = _addressAttributeParser.GetAttributeWarnings(customAttributes);
            foreach (var error in customAttributeWarnings)
            {
                ModelState.AddModelError("", error);
            }

            var newAddress = model.ShippingNewAddress;

            if (ModelState.IsValid)
            {
                //try to find an address with the same values (don't duplicate records)
                var address = _addressService.FindAddress(_customerService.GetAddressesByCustomerId(_workContext.CurrentCustomer.Id).ToList(),
                    newAddress.FirstName, newAddress.LastName, newAddress.PhoneNumber,
                    newAddress.Email, newAddress.FaxNumber, newAddress.Company,
                    newAddress.Address1, newAddress.Address2, newAddress.City,
                    newAddress.County, newAddress.StateProvinceId, newAddress.ZipPostalCode,
                    newAddress.CountryId, customAttributes);

                if (address == null)
                {
                    address = newAddress.ToEntity();
                    address.CustomAttributes = customAttributes;
                    address.CreatedOnUtc = DateTime.UtcNow;
                    //some validation
                    if (address.CountryId == 0)
                        address.CountryId = null;
                    if (address.StateProvinceId == 0)
                        address.StateProvinceId = null;

                    _addressService.InsertAddress(address);

                    _customerService.InsertCustomerAddress(_workContext.CurrentCustomer, address);

                }

                _workContext.CurrentCustomer.ShippingAddressId = address.Id;
                _customerService.UpdateCustomer(_workContext.CurrentCustomer);

                return RedirectToRoute("CheckoutShippingMethod");
            }

            //If we got this far, something failed, redisplay form
            model = _checkoutModelFactory.PrepareShippingAddressModel(cart,
                selectedCountryId: newAddress.CountryId,
                overrideAttributesXml: customAttributes);
            return View(model);
        }

        public virtual IActionResult ShippingMethod()
        {
            //validation
            if (_orderSettings.CheckoutDisabled)
                return RedirectToRoute("ShoppingCart");

            var cart = _shoppingCartService.GetShoppingCart(_workContext.CurrentCustomer, ShoppingCartType.ShoppingCart, _storeContext.CurrentStore.Id);

            if (!cart.Any())
                return RedirectToRoute("ShoppingCart");

            if (_orderSettings.OnePageCheckoutEnabled)
                return RedirectToRoute("CheckoutOnePage");

            if (_customerService.IsGuest(_workContext.CurrentCustomer) && !_orderSettings.AnonymousCheckoutAllowed)
                return Challenge();

            if (!_shoppingCartService.ShoppingCartRequiresShipping(cart))
            {
                _genericAttributeService.SaveAttribute<ShippingOption>(_workContext.CurrentCustomer, NopCustomerDefaults.SelectedShippingOptionAttribute, null, _storeContext.CurrentStore.Id);
                return RedirectToRoute("CheckoutPaymentMethod");
            }

            //model
            var model = _checkoutModelFactory.PrepareShippingMethodModel(cart, _customerService.GetCustomerShippingAddress(_workContext.CurrentCustomer));

            if (_shippingSettings.BypassShippingMethodSelectionIfOnlyOne &&
                model.ShippingMethods.Count == 1)
            {
                //if we have only one shipping method, then a customer doesn't have to choose a shipping method
                _genericAttributeService.SaveAttribute(_workContext.CurrentCustomer,
                    NopCustomerDefaults.SelectedShippingOptionAttribute,
                    model.ShippingMethods.First().ShippingOption,
                    _storeContext.CurrentStore.Id);

                return RedirectToRoute("CheckoutPaymentMethod");
            }

            return View(model);
        }

        [HttpPost, ActionName("ShippingMethod")]
        [FormValueRequired("nextstep")]
        public virtual IActionResult SelectShippingMethod(string shippingoption, IFormCollection form)
        {
            //validation
            if (_orderSettings.CheckoutDisabled)
                return RedirectToRoute("ShoppingCart");

            var cart = _shoppingCartService.GetShoppingCart(_workContext.CurrentCustomer, ShoppingCartType.ShoppingCart, _storeContext.CurrentStore.Id);

            if (!cart.Any())
                return RedirectToRoute("ShoppingCart");

            if (_orderSettings.OnePageCheckoutEnabled)
                return RedirectToRoute("CheckoutOnePage");

            if (_customerService.IsGuest(_workContext.CurrentCustomer) && !_orderSettings.AnonymousCheckoutAllowed)
                return Challenge();

            if (!_shoppingCartService.ShoppingCartRequiresShipping(cart))
            {
                _genericAttributeService.SaveAttribute<ShippingOption>(_workContext.CurrentCustomer,
                    NopCustomerDefaults.SelectedShippingOptionAttribute, null, _storeContext.CurrentStore.Id);
                return RedirectToRoute("CheckoutPaymentMethod");
            }

            //pickup point
            if (_shippingSettings.AllowPickupInStore && _orderSettings.DisplayPickupInStoreOnShippingMethodPage)
            {
                var pickupInStore = ParsePickupInStore(form);
                if (pickupInStore)
                {
                    var pickupOption = ParsePickupOption(form);
                    SavePickupOption(pickupOption);

                    return RedirectToRoute("CheckoutPaymentMethod");
                }

                //set value indicating that "pick up in store" option has not been chosen
                _genericAttributeService.SaveAttribute<PickupPoint>(_workContext.CurrentCustomer, NopCustomerDefaults.SelectedPickupPointAttribute, null, _storeContext.CurrentStore.Id);
            }

            //parse selected method 
            if (string.IsNullOrEmpty(shippingoption))
                return ShippingMethod();
            var splittedOption = shippingoption.Split(new[] { "___" }, StringSplitOptions.RemoveEmptyEntries);
            if (splittedOption.Length != 2)
                return ShippingMethod();
            var selectedName = splittedOption[0];
            var shippingRateComputationMethodSystemName = splittedOption[1];

            //find it
            //performance optimization. try cache first
            var shippingOptions = _genericAttributeService.GetAttribute<List<ShippingOption>>(_workContext.CurrentCustomer,
                NopCustomerDefaults.OfferedShippingOptionsAttribute, _storeContext.CurrentStore.Id);
            if (shippingOptions == null || !shippingOptions.Any())
            {
                //not found? let's load them using shipping service
                shippingOptions = _shippingService.GetShippingOptions(cart, _customerService.GetCustomerShippingAddress(_workContext.CurrentCustomer),
                    _workContext.CurrentCustomer, shippingRateComputationMethodSystemName, _storeContext.CurrentStore.Id).ShippingOptions.ToList();
            }
            else
            {
                //loaded cached results. let's filter result by a chosen shipping rate computation method
                shippingOptions = shippingOptions.Where(so => so.ShippingRateComputationMethodSystemName.Equals(shippingRateComputationMethodSystemName, StringComparison.InvariantCultureIgnoreCase))
                    .ToList();
            }

            var shippingOption = shippingOptions
                .Find(so => !string.IsNullOrEmpty(so.Name) && so.Name.Equals(selectedName, StringComparison.InvariantCultureIgnoreCase));
            if (shippingOption == null)
                return ShippingMethod();

            //save
            _genericAttributeService.SaveAttribute(_workContext.CurrentCustomer, NopCustomerDefaults.SelectedShippingOptionAttribute, shippingOption, _storeContext.CurrentStore.Id);

            return RedirectToRoute("CheckoutPaymentMethod");
        }

        public virtual IActionResult PaymentMethod()
        {
            //validation
            if (_orderSettings.CheckoutDisabled)
                return RedirectToRoute("ShoppingCart");

            var cart = _shoppingCartService.GetShoppingCart(_workContext.CurrentCustomer, ShoppingCartType.ShoppingCart, _storeContext.CurrentStore.Id);

            if (!cart.Any())
                return RedirectToRoute("ShoppingCart");

            if (_orderSettings.OnePageCheckoutEnabled)
                return RedirectToRoute("CheckoutOnePage");

            if (_customerService.IsGuest(_workContext.CurrentCustomer) && !_orderSettings.AnonymousCheckoutAllowed)
                return Challenge();

            //Check whether payment workflow is required
            //we ignore reward points during cart total calculation
            var isPaymentWorkflowRequired = _orderProcessingService.IsPaymentWorkflowRequired(cart, false);
            if (!isPaymentWorkflowRequired)
            {
                _genericAttributeService.SaveAttribute<string>(_workContext.CurrentCustomer,
                    NopCustomerDefaults.SelectedPaymentMethodAttribute, null, _storeContext.CurrentStore.Id);
                return RedirectToRoute("CheckoutPaymentInfo");
            }

            //filter by country
            var filterByCountryId = 0;
            if (_addressSettings.CountryEnabled)
            {
                filterByCountryId = _customerService.GetCustomerBillingAddress(_workContext.CurrentCustomer)?.CountryId ?? 0;
            }

            //model
            var paymentMethodModel = _checkoutModelFactory.PreparePaymentMethodModel(cart, filterByCountryId);

            if (_paymentSettings.BypassPaymentMethodSelectionIfOnlyOne &&
                paymentMethodModel.PaymentMethods.Count == 1 && !paymentMethodModel.DisplayRewardPoints)
            {
                //if we have only one payment method and reward points are disabled or the current customer doesn't have any reward points
                //so customer doesn't have to choose a payment method

                _genericAttributeService.SaveAttribute(_workContext.CurrentCustomer,
                    NopCustomerDefaults.SelectedPaymentMethodAttribute,
                    paymentMethodModel.PaymentMethods[0].PaymentMethodSystemName,
                    _storeContext.CurrentStore.Id);
                return RedirectToRoute("CheckoutPaymentInfo");
            }

            return View(paymentMethodModel);
        }

        [HttpPost, ActionName("PaymentMethod")]
        [FormValueRequired("nextstep")]
        public virtual IActionResult SelectPaymentMethod(string paymentmethod, CheckoutPaymentMethodModel model)
        {
            //validation
            if (_orderSettings.CheckoutDisabled)
                return RedirectToRoute("ShoppingCart");

            var cart = _shoppingCartService.GetShoppingCart(_workContext.CurrentCustomer, ShoppingCartType.ShoppingCart, _storeContext.CurrentStore.Id);

            if (!cart.Any())
                return RedirectToRoute("ShoppingCart");

            if (_orderSettings.OnePageCheckoutEnabled)
                return RedirectToRoute("CheckoutOnePage");

            if (_customerService.IsGuest(_workContext.CurrentCustomer) && !_orderSettings.AnonymousCheckoutAllowed)
                return Challenge();

            //reward points
            if (_rewardPointsSettings.Enabled)
            {
                _genericAttributeService.SaveAttribute(_workContext.CurrentCustomer,
                    NopCustomerDefaults.UseRewardPointsDuringCheckoutAttribute, model.UseRewardPoints,
                    _storeContext.CurrentStore.Id);
            }

            //Check whether payment workflow is required
            var isPaymentWorkflowRequired = _orderProcessingService.IsPaymentWorkflowRequired(cart);
            if (!isPaymentWorkflowRequired)
            {
                _genericAttributeService.SaveAttribute<string>(_workContext.CurrentCustomer,
                    NopCustomerDefaults.SelectedPaymentMethodAttribute, null, _storeContext.CurrentStore.Id);
                return RedirectToRoute("CheckoutPaymentInfo");
            }
            //payment method 
            if (string.IsNullOrEmpty(paymentmethod))
                return PaymentMethod();

            if (!_paymentPluginManager.IsPluginActive(paymentmethod, _workContext.CurrentCustomer, _storeContext.CurrentStore.Id))
                return PaymentMethod();

            //save
            _genericAttributeService.SaveAttribute(_workContext.CurrentCustomer,
                NopCustomerDefaults.SelectedPaymentMethodAttribute, paymentmethod, _storeContext.CurrentStore.Id);

            return RedirectToRoute("CheckoutPaymentInfo");
        }

        public virtual IActionResult PaymentInfo()
        {
            //validation
            if (_orderSettings.CheckoutDisabled)
                return RedirectToRoute("ShoppingCart");

            var cart = _shoppingCartService.GetShoppingCart(_workContext.CurrentCustomer, ShoppingCartType.ShoppingCart, _storeContext.CurrentStore.Id);

            if (!cart.Any())
                return RedirectToRoute("ShoppingCart");

            if (_orderSettings.OnePageCheckoutEnabled)
                return RedirectToRoute("CheckoutOnePage");

            if (_customerService.IsGuest(_workContext.CurrentCustomer) && !_orderSettings.AnonymousCheckoutAllowed)
                return Challenge();

            //Check whether payment workflow is required
            var isPaymentWorkflowRequired = _orderProcessingService.IsPaymentWorkflowRequired(cart);
            if (!isPaymentWorkflowRequired)
            {
                return RedirectToRoute("CheckoutConfirm");
            }

            //load payment method
            var paymentMethodSystemName = _genericAttributeService.GetAttribute<string>(_workContext.CurrentCustomer,
                NopCustomerDefaults.SelectedPaymentMethodAttribute, _storeContext.CurrentStore.Id);
            var paymentMethod = _paymentPluginManager
                .LoadPluginBySystemName(paymentMethodSystemName, _workContext.CurrentCustomer, _storeContext.CurrentStore.Id);
            if (paymentMethod == null)
                return RedirectToRoute("CheckoutPaymentMethod");

            //Check whether payment info should be skipped
            if (paymentMethod.SkipPaymentInfo ||
                (paymentMethod.PaymentMethodType == PaymentMethodType.Redirection && _paymentSettings.SkipPaymentInfoStepForRedirectionPaymentMethods))
            {
                //skip payment info page
                var paymentInfo = new ProcessPaymentRequest();

                //session save
                HttpContext.Session.Set("OrderPaymentInfo", paymentInfo);

                return RedirectToRoute("CheckoutConfirm");
            }

            //model
            var model = _checkoutModelFactory.PreparePaymentInfoModel(paymentMethod);
            return View(model);
        }

        [HttpPost, ActionName("PaymentInfo")]
        [FormValueRequired("nextstep")]
        public virtual IActionResult EnterPaymentInfo(IFormCollection form)
        {
            //validation
            if (_orderSettings.CheckoutDisabled)
                return RedirectToRoute("ShoppingCart");

            var cart = _shoppingCartService.GetShoppingCart(_workContext.CurrentCustomer, ShoppingCartType.ShoppingCart, _storeContext.CurrentStore.Id);

            if (!cart.Any())
                return RedirectToRoute("ShoppingCart");

            if (_orderSettings.OnePageCheckoutEnabled)
                return RedirectToRoute("CheckoutOnePage");

            if (_customerService.IsGuest(_workContext.CurrentCustomer) && !_orderSettings.AnonymousCheckoutAllowed)
                return Challenge();

            //Check whether payment workflow is required
            var isPaymentWorkflowRequired = _orderProcessingService.IsPaymentWorkflowRequired(cart);
            if (!isPaymentWorkflowRequired)
            {
                return RedirectToRoute("CheckoutConfirm");
            }

            //load payment method
            var paymentMethodSystemName = _genericAttributeService.GetAttribute<string>(_workContext.CurrentCustomer,
                NopCustomerDefaults.SelectedPaymentMethodAttribute, _storeContext.CurrentStore.Id);
            var paymentMethod = _paymentPluginManager
                .LoadPluginBySystemName(paymentMethodSystemName, _workContext.CurrentCustomer, _storeContext.CurrentStore.Id);
            if (paymentMethod == null)
                return RedirectToRoute("CheckoutPaymentMethod");

            var warnings = paymentMethod.ValidatePaymentForm(form);
            foreach (var warning in warnings)
                ModelState.AddModelError("", warning);
            if (ModelState.IsValid)
            {
                //get payment info
                var paymentInfo = paymentMethod.GetPaymentInfo(form);
                //set previous order GUID (if exists)
                _paymentService.GenerateOrderGuid(paymentInfo);

                //session save
                HttpContext.Session.Set("OrderPaymentInfo", paymentInfo);
                return RedirectToRoute("CheckoutConfirm");
            }

            //If we got this far, something failed, redisplay form
            //model
            var model = _checkoutModelFactory.PreparePaymentInfoModel(paymentMethod);
            return View(model);
        }

        public virtual IActionResult Confirm()
        {
            //validation
            if (_orderSettings.CheckoutDisabled)
                return RedirectToRoute("ShoppingCart");

            var cart = _shoppingCartService.GetShoppingCart(_workContext.CurrentCustomer, ShoppingCartType.ShoppingCart, _storeContext.CurrentStore.Id);

            if (!cart.Any())
                return RedirectToRoute("ShoppingCart");

            if (_orderSettings.OnePageCheckoutEnabled)
                return RedirectToRoute("CheckoutOnePage");

            if (_customerService.IsGuest(_workContext.CurrentCustomer) && !_orderSettings.AnonymousCheckoutAllowed)
                return Challenge();

            //model
            var model = _checkoutModelFactory.PrepareConfirmOrderModel(cart);
            return View(model);
        }

        [HttpPost, ActionName("Confirm")]
        public virtual IActionResult ConfirmOrder()
        {
            //validation
            if (_orderSettings.CheckoutDisabled)
                return RedirectToRoute("ShoppingCart");

            var cart = _shoppingCartService.GetShoppingCart(_workContext.CurrentCustomer, ShoppingCartType.ShoppingCart, _storeContext.CurrentStore.Id);

            if (!cart.Any())
                return RedirectToRoute("ShoppingCart");

            if (_orderSettings.OnePageCheckoutEnabled)
                return RedirectToRoute("CheckoutOnePage");

            if (_customerService.IsGuest(_workContext.CurrentCustomer) && !_orderSettings.AnonymousCheckoutAllowed)
                return Challenge();

            //model
            var model = _checkoutModelFactory.PrepareConfirmOrderModel(cart);
            try
            {
                //prevent 2 orders being placed within an X seconds time frame
                if (!IsMinimumOrderPlacementIntervalValid(_workContext.CurrentCustomer))
                    throw new Exception(_localizationService.GetResource("Checkout.MinOrderPlacementInterval"));

                //place order
                var processPaymentRequest = HttpContext.Session.Get<ProcessPaymentRequest>("OrderPaymentInfo");
                if (processPaymentRequest == null)
                {
                    //Check whether payment workflow is required
                    if (_orderProcessingService.IsPaymentWorkflowRequired(cart))
                        return RedirectToRoute("CheckoutPaymentInfo");

                    processPaymentRequest = new ProcessPaymentRequest();
                }
                _paymentService.GenerateOrderGuid(processPaymentRequest);
                processPaymentRequest.StoreId = _storeContext.CurrentStore.Id;
                processPaymentRequest.CustomerId = _workContext.CurrentCustomer.Id;
                processPaymentRequest.PaymentMethodSystemName = _genericAttributeService.GetAttribute<string>(_workContext.CurrentCustomer,
                    NopCustomerDefaults.SelectedPaymentMethodAttribute, _storeContext.CurrentStore.Id);
                HttpContext.Session.Set<ProcessPaymentRequest>("OrderPaymentInfo", processPaymentRequest);
                var placeOrderResult = _orderProcessingService.PlaceOrder(processPaymentRequest);
                if (placeOrderResult.Success)
                {
                    HttpContext.Session.Set<ProcessPaymentRequest>("OrderPaymentInfo", null);
                    var postProcessPaymentRequest = new PostProcessPaymentRequest
                    {
                        Order = placeOrderResult.PlacedOrder
                    };
                    _paymentService.PostProcessPayment(postProcessPaymentRequest);

                    if (_webHelper.IsRequestBeingRedirected || _webHelper.IsPostBeingDone)
                    {
                        //redirection or POST has been done in PostProcessPayment
                        return Content(_localizationService.GetResource("Checkout.RedirectMessage"));
                    }

                    return RedirectToRoute("CheckoutCompleted", new { orderId = placeOrderResult.PlacedOrder.Id });
                }

                foreach (var error in placeOrderResult.Errors)
                    model.Warnings.Add(error);
            }
            catch (Exception exc)
            {
                _logger.Warning(exc.Message, exc);
                model.Warnings.Add(exc.Message);
            }

            //If we got this far, something failed, redisplay form
            return View(model);
        }

        #endregion

        #region Methods (one page checkout)

        protected virtual JsonResult OpcLoadStepAfterShippingAddress(IList<ShoppingCartItem> cart)
        {
            var shippingMethodModel = _checkoutModelFactory.PrepareShippingMethodModel(cart, _customerService.GetCustomerShippingAddress(_workContext.CurrentCustomer));
            if (_shippingSettings.BypassShippingMethodSelectionIfOnlyOne &&
                shippingMethodModel.ShippingMethods.Count == 1)
            {
                //if we have only one shipping method, then a customer doesn't have to choose a shipping method
                _genericAttributeService.SaveAttribute(_workContext.CurrentCustomer,
                    NopCustomerDefaults.SelectedShippingOptionAttribute,
                    shippingMethodModel.ShippingMethods.First().ShippingOption,
                    _storeContext.CurrentStore.Id);

                //load next step
                return OpcLoadStepAfterShippingMethod(cart);
            }

            return Json(new
            {
                update_section = new UpdateSectionJsonModel
                {
                    name = "shipping-method",
                    html = RenderPartialViewToString("OpcShippingMethods", shippingMethodModel)
                },
                goto_section = "shipping_method"
            });
        }

        protected virtual JsonResult OpcLoadStepAfterShippingMethod(IList<ShoppingCartItem> cart)
        {
            //Check whether payment workflow is required
            //we ignore reward points during cart total calculation
            var isPaymentWorkflowRequired = _orderProcessingService.IsPaymentWorkflowRequired(cart, false);
            if (isPaymentWorkflowRequired)
            {
                //filter by country
                var filterByCountryId = 0;
                if (_addressSettings.CountryEnabled)
                {
                    filterByCountryId = _customerService.GetCustomerBillingAddress(_workContext.CurrentCustomer)?.CountryId ?? 0;
                }

                //payment is required
                var paymentMethodModel = _checkoutModelFactory.PreparePaymentMethodModel(cart, filterByCountryId);

                if (_paymentSettings.BypassPaymentMethodSelectionIfOnlyOne &&
                    paymentMethodModel.PaymentMethods.Count == 1 && !paymentMethodModel.DisplayRewardPoints)
                {
                    //if we have only one payment method and reward points are disabled or the current customer doesn't have any reward points
                    //so customer doesn't have to choose a payment method

                    var selectedPaymentMethodSystemName = paymentMethodModel.PaymentMethods[0].PaymentMethodSystemName;
                    _genericAttributeService.SaveAttribute(_workContext.CurrentCustomer,
                        NopCustomerDefaults.SelectedPaymentMethodAttribute,
                        selectedPaymentMethodSystemName, _storeContext.CurrentStore.Id);

                    var paymentMethodInst = _paymentPluginManager
                        .LoadPluginBySystemName(selectedPaymentMethodSystemName, _workContext.CurrentCustomer, _storeContext.CurrentStore.Id);
                    if (!_paymentPluginManager.IsPluginActive(paymentMethodInst))
                        throw new Exception("Selected payment method can't be parsed");

                    return OpcLoadStepAfterPaymentMethod(paymentMethodInst, cart);
                }

                //customer have to choose a payment method
                return Json(new
                {
                    update_section = new UpdateSectionJsonModel
                    {
                        name = "payment-method",
                        html = RenderPartialViewToString("OpcPaymentMethods", paymentMethodModel)
                    },
                    goto_section = "payment_method"
                });
            }

            //payment is not required
            _genericAttributeService.SaveAttribute<string>(_workContext.CurrentCustomer,
                NopCustomerDefaults.SelectedPaymentMethodAttribute, null, _storeContext.CurrentStore.Id);

            var confirmOrderModel = _checkoutModelFactory.PrepareConfirmOrderModel(cart);
            return Json(new
            {
                update_section = new UpdateSectionJsonModel
                {
                    name = "confirm-order",
                    html = RenderPartialViewToString("OpcConfirmOrder", confirmOrderModel)
                },
                goto_section = "confirm_order"
            });
        }

        protected virtual JsonResult OpcLoadStepAfterPaymentMethod(IPaymentMethod paymentMethod, IList<ShoppingCartItem> cart)
        {
            if (paymentMethod.SkipPaymentInfo ||
                (paymentMethod.PaymentMethodType == PaymentMethodType.Redirection && _paymentSettings.SkipPaymentInfoStepForRedirectionPaymentMethods))
            {
                //skip payment info page
                var paymentInfo = new ProcessPaymentRequest();

                //session save
                HttpContext.Session.Set("OrderPaymentInfo", paymentInfo);

                var confirmOrderModel = _checkoutModelFactory.PrepareConfirmOrderModel(cart);
                return Json(new
                {
                    update_section = new UpdateSectionJsonModel
                    {
                        name = "confirm-order",
                        html = RenderPartialViewToString("OpcConfirmOrder", confirmOrderModel)
                    },
                    goto_section = "confirm_order"
                });
            }

            //return payment info page
            var paymenInfoModel = _checkoutModelFactory.PreparePaymentInfoModel(paymentMethod);
            return Json(new
            {
                update_section = new UpdateSectionJsonModel
                {
                    name = "payment-info",
                    html = RenderPartialViewToString("OpcPaymentInfo", paymenInfoModel)
                },
                goto_section = "payment_info"
            });
        }

        public virtual IActionResult OnePageCheckout()
        {
            //validation
            if (_orderSettings.CheckoutDisabled)
                return RedirectToRoute("ShoppingCart");

            var cart = _shoppingCartService.GetShoppingCart(_workContext.CurrentCustomer, ShoppingCartType.ShoppingCart, _storeContext.CurrentStore.Id);

            if (!cart.Any())
                return RedirectToRoute("ShoppingCart");

            if (!_orderSettings.OnePageCheckoutEnabled)
                return RedirectToRoute("Checkout");

            if (_customerService.IsGuest(_workContext.CurrentCustomer) && !_orderSettings.AnonymousCheckoutAllowed)
                return Challenge();

            var model = _checkoutModelFactory.PrepareOnePageCheckoutModel(cart);
            return View(model);
        }

        [IgnoreAntiforgeryToken]
        public virtual IActionResult OpcSaveBilling(CheckoutBillingAddressModel model, IFormCollection form)
        {
            try
            {
                //validation
                if (_orderSettings.CheckoutDisabled)
                    throw new Exception(_localizationService.GetResource("Checkout.Disabled"));

                var cart = _shoppingCartService.GetShoppingCart(_workContext.CurrentCustomer, ShoppingCartType.ShoppingCart, _storeContext.CurrentStore.Id);

                if (!cart.Any())
                    throw new Exception("Your cart is empty");

                if (!_orderSettings.OnePageCheckoutEnabled)
                    throw new Exception("One page checkout is disabled");

                if (_customerService.IsGuest(_workContext.CurrentCustomer) && !_orderSettings.AnonymousCheckoutAllowed)
                    throw new Exception("Anonymous checkout is not allowed");

                int.TryParse(form["billing_address_id"], out var billingAddressId);

                if (billingAddressId > 0)
                {
                    //existing address
                    var address = _customerService.GetCustomerAddress(_workContext.CurrentCustomer.Id, billingAddressId)
                        ?? throw new Exception(_localizationService.GetResource("Checkout.Address.NotFound"));

                    _workContext.CurrentCustomer.BillingAddressId = address.Id;
                    _customerService.UpdateCustomer(_workContext.CurrentCustomer);
                }
                else
                {
                    //new address
                    var newAddress = model.BillingNewAddress;

                    //custom address attributes
                    var customAttributes = _addressAttributeParser.ParseCustomAddressAttributes(form);
                    var customAttributeWarnings = _addressAttributeParser.GetAttributeWarnings(customAttributes);
                    foreach (var error in customAttributeWarnings)
                    {
                        ModelState.AddModelError("", error);
                    }

                    //validate model
                    if (!ModelState.IsValid)
                    {
                        //model is not valid. redisplay the form with errors
                        var billingAddressModel = _checkoutModelFactory.PrepareBillingAddressModel(cart,
                            selectedCountryId: newAddress.CountryId,
                            overrideAttributesXml: customAttributes);
                        billingAddressModel.NewAddressPreselected = true;
                        return Json(new
                        {
                            update_section = new UpdateSectionJsonModel
                            {
                                name = "billing",
                                html = RenderPartialViewToString("OpcBillingAddress", billingAddressModel)
                            },
                            wrong_billing_address = true,
                        });
                    }

                    //try to find an address with the same values (don't duplicate records)
                    var address = _addressService.FindAddress(_customerService.GetAddressesByCustomerId(_workContext.CurrentCustomer.Id).ToList(),
                        newAddress.FirstName, newAddress.LastName, newAddress.PhoneNumber,
                        newAddress.Email, newAddress.FaxNumber, newAddress.Company,
                        newAddress.Address1, newAddress.Address2, newAddress.City,
                        newAddress.County, newAddress.StateProvinceId, newAddress.ZipPostalCode,
                        newAddress.CountryId, customAttributes);

                    if (address == null)
                    {
                        //address is not found. let's create a new one
                        address = newAddress.ToEntity();
                        address.CustomAttributes = customAttributes;
                        address.CreatedOnUtc = DateTime.UtcNow;

                        //some validation
                        if (address.CountryId == 0)
                            address.CountryId = null;

                        if (address.StateProvinceId == 0)
                            address.StateProvinceId = null;

                        _addressService.InsertAddress(address);

                        _customerService.InsertCustomerAddress(_workContext.CurrentCustomer, address);
                    }

                    _workContext.CurrentCustomer.BillingAddressId = address.Id;

                    _customerService.UpdateCustomer(_workContext.CurrentCustomer);
                }

                if (_shoppingCartService.ShoppingCartRequiresShipping(cart))
                {
                    //shipping is required
                    var address = _customerService.GetCustomerBillingAddress(_workContext.CurrentCustomer);

                    //by default Shipping is available if the country is not specified
                    var shippingAllowed = _addressSettings.CountryEnabled ? _countryService.GetCountryByAddress(address)?.AllowsShipping ?? false : true;
                    if (_shippingSettings.ShipToSameAddress && model.ShipToSameAddress && shippingAllowed)
                    {
                        //ship to the same address
                        _workContext.CurrentCustomer.ShippingAddressId = address.Id;
                        _customerService.UpdateCustomer(_workContext.CurrentCustomer);
                        //reset selected shipping method (in case if "pick up in store" was selected)
                        _genericAttributeService.SaveAttribute<ShippingOption>(_workContext.CurrentCustomer, NopCustomerDefaults.SelectedShippingOptionAttribute, null, _storeContext.CurrentStore.Id);
                        _genericAttributeService.SaveAttribute<PickupPoint>(_workContext.CurrentCustomer, NopCustomerDefaults.SelectedPickupPointAttribute, null, _storeContext.CurrentStore.Id);
                        //limitation - "Ship to the same address" doesn't properly work in "pick up in store only" case (when no shipping plugins are available) 
                        return OpcLoadStepAfterShippingAddress(cart);
                    }

                    //do not ship to the same address
                    var shippingAddressModel = _checkoutModelFactory.PrepareShippingAddressModel(cart, prePopulateNewAddressWithCustomerFields: true);

                    return Json(new
                    {
                        update_section = new UpdateSectionJsonModel
                        {
                            name = "shipping",
                            html = RenderPartialViewToString("OpcShippingAddress", shippingAddressModel)
                        },
                        goto_section = "shipping"
                    });
                }

                //shipping is not required
                _genericAttributeService.SaveAttribute<ShippingOption>(_workContext.CurrentCustomer, NopCustomerDefaults.SelectedShippingOptionAttribute, null, _storeContext.CurrentStore.Id);

                //load next step
                return OpcLoadStepAfterShippingMethod(cart);
            }
            catch (Exception exc)
            {
                _logger.Warning(exc.Message, exc, _workContext.CurrentCustomer);
                return Json(new { error = 1, message = exc.Message });
            }
        }

        [IgnoreAntiforgeryToken]
        public virtual IActionResult OpcSaveShipping(CheckoutShippingAddressModel model, IFormCollection form)
        {
            try
            {
                //validation
                if (_orderSettings.CheckoutDisabled)
                    throw new Exception(_localizationService.GetResource("Checkout.Disabled"));

                var cart = _shoppingCartService.GetShoppingCart(_workContext.CurrentCustomer, ShoppingCartType.ShoppingCart, _storeContext.CurrentStore.Id);

                if (!cart.Any())
                    throw new Exception("Your cart is empty");

                if (!_orderSettings.OnePageCheckoutEnabled)
                    throw new Exception("One page checkout is disabled");

                if (_customerService.IsGuest(_workContext.CurrentCustomer) && !_orderSettings.AnonymousCheckoutAllowed)
                    throw new Exception("Anonymous checkout is not allowed");

                if (!_shoppingCartService.ShoppingCartRequiresShipping(cart))
                    throw new Exception("Shipping is not required");

                //pickup point
                if (_shippingSettings.AllowPickupInStore && !_orderSettings.DisplayPickupInStoreOnShippingMethodPage)
                {
                    var pickupInStore = ParsePickupInStore(form);
                    if (pickupInStore)
                    {
                        var pickupOption = ParsePickupOption(form);
                        SavePickupOption(pickupOption);

                        return OpcLoadStepAfterShippingMethod(cart);
                    }

                    //set value indicating that "pick up in store" option has not been chosen
                    _genericAttributeService.SaveAttribute<PickupPoint>(_workContext.CurrentCustomer, NopCustomerDefaults.SelectedPickupPointAttribute, null, _storeContext.CurrentStore.Id);
                }

                int.TryParse(form["shipping_address_id"], out var shippingAddressId);

                if (shippingAddressId > 0)
                {
                    //existing address
                    var address = _customerService.GetCustomerAddress(_workContext.CurrentCustomer.Id, shippingAddressId)
                        ?? throw new Exception(_localizationService.GetResource("Checkout.Address.NotFound"));

                    _workContext.CurrentCustomer.ShippingAddressId = address.Id;
                    _customerService.UpdateCustomer(_workContext.CurrentCustomer);
                }
                else
                {
                    //new address
                    var newAddress = model.ShippingNewAddress;

                    //custom address attributes
                    var customAttributes = _addressAttributeParser.ParseCustomAddressAttributes(form);
                    var customAttributeWarnings = _addressAttributeParser.GetAttributeWarnings(customAttributes);
                    foreach (var error in customAttributeWarnings)
                    {
                        ModelState.AddModelError("", error);
                    }

                    //validate model
                    if (!ModelState.IsValid)
                    {
                        //model is not valid. redisplay the form with errors
                        var shippingAddressModel = _checkoutModelFactory.PrepareShippingAddressModel(cart,
                            selectedCountryId: newAddress.CountryId,
                            overrideAttributesXml: customAttributes);
                        shippingAddressModel.NewAddressPreselected = true;
                        return Json(new
                        {
                            update_section = new UpdateSectionJsonModel
                            {
                                name = "shipping",
                                html = RenderPartialViewToString("OpcShippingAddress", shippingAddressModel)
                            }
                        });
                    }

                    //try to find an address with the same values (don't duplicate records)
                    var address = _addressService.FindAddress(_customerService.GetAddressesByCustomerId(_workContext.CurrentCustomer.Id).ToList(),
                        newAddress.FirstName, newAddress.LastName, newAddress.PhoneNumber,
                        newAddress.Email, newAddress.FaxNumber, newAddress.Company,
                        newAddress.Address1, newAddress.Address2, newAddress.City,
                        newAddress.County, newAddress.StateProvinceId, newAddress.ZipPostalCode,
                        newAddress.CountryId, customAttributes);

                    if (address == null)
                    {
                        address = newAddress.ToEntity();
                        address.CustomAttributes = customAttributes;
                        address.CreatedOnUtc = DateTime.UtcNow;

                        _addressService.InsertAddress(address);

                        _customerService.InsertCustomerAddress(_workContext.CurrentCustomer, address);
                    }

                    _workContext.CurrentCustomer.ShippingAddressId = address.Id;

                    _customerService.UpdateCustomer(_workContext.CurrentCustomer);
                }

                return OpcLoadStepAfterShippingAddress(cart);
            }
            catch (Exception exc)
            {
                _logger.Warning(exc.Message, exc, _workContext.CurrentCustomer);
                return Json(new { error = 1, message = exc.Message });
            }
        }

        [IgnoreAntiforgeryToken]
        public virtual IActionResult OpcSaveShippingMethod(string shippingoption, IFormCollection form)
        {
            try
            {
                //validation
                if (_orderSettings.CheckoutDisabled)
                    throw new Exception(_localizationService.GetResource("Checkout.Disabled"));

                var cart = _shoppingCartService.GetShoppingCart(_workContext.CurrentCustomer, ShoppingCartType.ShoppingCart, _storeContext.CurrentStore.Id);

                if (!cart.Any())
                    throw new Exception("Your cart is empty");

                if (!_orderSettings.OnePageCheckoutEnabled)
                    throw new Exception("One page checkout is disabled");

                if (_customerService.IsGuest(_workContext.CurrentCustomer) && !_orderSettings.AnonymousCheckoutAllowed)
                    throw new Exception("Anonymous checkout is not allowed");

                if (!_shoppingCartService.ShoppingCartRequiresShipping(cart))
                    throw new Exception("Shipping is not required");

                //pickup point
                if (_shippingSettings.AllowPickupInStore && _orderSettings.DisplayPickupInStoreOnShippingMethodPage)
                {
                    var pickupInStore = ParsePickupInStore(form);
                    if (pickupInStore)
                    {
                        var pickupOption = ParsePickupOption(form);
                        SavePickupOption(pickupOption);

                        return OpcLoadStepAfterShippingMethod(cart);
                    }

                    //set value indicating that "pick up in store" option has not been chosen
                    _genericAttributeService.SaveAttribute<PickupPoint>(_workContext.CurrentCustomer, NopCustomerDefaults.SelectedPickupPointAttribute, null, _storeContext.CurrentStore.Id);
                }

                //parse selected method 
                if (string.IsNullOrEmpty(shippingoption))
                    throw new Exception("Selected shipping method can't be parsed");
                var splittedOption = shippingoption.Split(new[] { "___" }, StringSplitOptions.RemoveEmptyEntries);
                if (splittedOption.Length != 2)
                    throw new Exception("Selected shipping method can't be parsed");
                var selectedName = splittedOption[0];
                var shippingRateComputationMethodSystemName = splittedOption[1];

                //find it
                //performance optimization. try cache first
                var shippingOptions = _genericAttributeService.GetAttribute<List<ShippingOption>>(_workContext.CurrentCustomer,
                    NopCustomerDefaults.OfferedShippingOptionsAttribute, _storeContext.CurrentStore.Id);
                if (shippingOptions == null || !shippingOptions.Any())
                {
                    //not found? let's load them using shipping service
                    shippingOptions = _shippingService.GetShippingOptions(cart, _customerService.GetCustomerShippingAddress(_workContext.CurrentCustomer),
                        _workContext.CurrentCustomer, shippingRateComputationMethodSystemName, _storeContext.CurrentStore.Id).ShippingOptions.ToList();
                }
                else
                {
                    //loaded cached results. let's filter result by a chosen shipping rate computation method
                    shippingOptions = shippingOptions.Where(so => so.ShippingRateComputationMethodSystemName.Equals(shippingRateComputationMethodSystemName, StringComparison.InvariantCultureIgnoreCase))
                        .ToList();
                }

                var shippingOption = shippingOptions
                    .Find(so => !string.IsNullOrEmpty(so.Name) && so.Name.Equals(selectedName, StringComparison.InvariantCultureIgnoreCase));
                if (shippingOption == null)
                    throw new Exception("Selected shipping method can't be loaded");

                //save
                _genericAttributeService.SaveAttribute(_workContext.CurrentCustomer, NopCustomerDefaults.SelectedShippingOptionAttribute, shippingOption, _storeContext.CurrentStore.Id);

                //load next step
                return OpcLoadStepAfterShippingMethod(cart);
            }
            catch (Exception exc)
            {
                _logger.Warning(exc.Message, exc, _workContext.CurrentCustomer);
                return Json(new { error = 1, message = exc.Message });
            }
        }

        [IgnoreAntiforgeryToken]
        public virtual IActionResult OpcSavePaymentMethod(string paymentmethod, CheckoutPaymentMethodModel model)
        {
            try
            {
                //validation
                if (_orderSettings.CheckoutDisabled)
                    throw new Exception(_localizationService.GetResource("Checkout.Disabled"));

                var cart = _shoppingCartService.GetShoppingCart(_workContext.CurrentCustomer, ShoppingCartType.ShoppingCart, _storeContext.CurrentStore.Id);

                if (!cart.Any())
                    throw new Exception("Your cart is empty");

                if (!_orderSettings.OnePageCheckoutEnabled)
                    throw new Exception("One page checkout is disabled");

                if (_customerService.IsGuest(_workContext.CurrentCustomer) && !_orderSettings.AnonymousCheckoutAllowed)
                    throw new Exception("Anonymous checkout is not allowed");

                //payment method 
                if (string.IsNullOrEmpty(paymentmethod))
                    throw new Exception("Selected payment method can't be parsed");

                //reward points
                if (_rewardPointsSettings.Enabled)
                {
                    _genericAttributeService.SaveAttribute(_workContext.CurrentCustomer,
                        NopCustomerDefaults.UseRewardPointsDuringCheckoutAttribute, model.UseRewardPoints,
                        _storeContext.CurrentStore.Id);
                }

                //Check whether payment workflow is required
                var isPaymentWorkflowRequired = _orderProcessingService.IsPaymentWorkflowRequired(cart);
                if (!isPaymentWorkflowRequired)
                {
                    //payment is not required
                    _genericAttributeService.SaveAttribute<string>(_workContext.CurrentCustomer,
                        NopCustomerDefaults.SelectedPaymentMethodAttribute, null, _storeContext.CurrentStore.Id);

                    var confirmOrderModel = _checkoutModelFactory.PrepareConfirmOrderModel(cart);
                    return Json(new
                    {
                        update_section = new UpdateSectionJsonModel
                        {
                            name = "confirm-order",
                            html = RenderPartialViewToString("OpcConfirmOrder", confirmOrderModel)
                        },
                        goto_section = "confirm_order"
                    });
                }

                var paymentMethodInst = _paymentPluginManager
                    .LoadPluginBySystemName(paymentmethod, _workContext.CurrentCustomer, _storeContext.CurrentStore.Id);
                if (!_paymentPluginManager.IsPluginActive(paymentMethodInst))
                    throw new Exception("Selected payment method can't be parsed");

                //save
                _genericAttributeService.SaveAttribute(_workContext.CurrentCustomer,
                    NopCustomerDefaults.SelectedPaymentMethodAttribute, paymentmethod, _storeContext.CurrentStore.Id);

                return OpcLoadStepAfterPaymentMethod(paymentMethodInst, cart);
            }
            catch (Exception exc)
            {
                _logger.Warning(exc.Message, exc, _workContext.CurrentCustomer);
                return Json(new { error = 1, message = exc.Message });
            }
        }

        [IgnoreAntiforgeryToken]
        public virtual IActionResult OpcSavePaymentInfo(IFormCollection form)
        {
            try
            {
                //validation
                if (_orderSettings.CheckoutDisabled)
                    throw new Exception(_localizationService.GetResource("Checkout.Disabled"));

                var cart = _shoppingCartService.GetShoppingCart(_workContext.CurrentCustomer, ShoppingCartType.ShoppingCart, _storeContext.CurrentStore.Id);

                if (!cart.Any())
                    throw new Exception("Your cart is empty");

                if (!_orderSettings.OnePageCheckoutEnabled)
                    throw new Exception("One page checkout is disabled");

                if (_customerService.IsGuest(_workContext.CurrentCustomer) && !_orderSettings.AnonymousCheckoutAllowed)
                    throw new Exception("Anonymous checkout is not allowed");

                var paymentMethodSystemName = _genericAttributeService.GetAttribute<string>(_workContext.CurrentCustomer,
                    NopCustomerDefaults.SelectedPaymentMethodAttribute, _storeContext.CurrentStore.Id);
                var paymentMethod = _paymentPluginManager
                    .LoadPluginBySystemName(paymentMethodSystemName, _workContext.CurrentCustomer, _storeContext.CurrentStore.Id)
                    ?? throw new Exception("Payment method is not selected");

                var warnings = paymentMethod.ValidatePaymentForm(form);
                foreach (var warning in warnings)
                    ModelState.AddModelError("", warning);
                if (ModelState.IsValid)
                {
                    //get payment info
                    var paymentInfo = paymentMethod.GetPaymentInfo(form);
                    //set previous order GUID (if exists)
                    _paymentService.GenerateOrderGuid(paymentInfo);

                    //session save
                    HttpContext.Session.Set("OrderPaymentInfo", paymentInfo);

                    var confirmOrderModel = _checkoutModelFactory.PrepareConfirmOrderModel(cart);
                    return Json(new
                    {
                        update_section = new UpdateSectionJsonModel
                        {
                            name = "confirm-order",
                            html = RenderPartialViewToString("OpcConfirmOrder", confirmOrderModel)
                        },
                        goto_section = "confirm_order"
                    });
                }

                //If we got this far, something failed, redisplay form
                var paymenInfoModel = _checkoutModelFactory.PreparePaymentInfoModel(paymentMethod);
                return Json(new
                {
                    update_section = new UpdateSectionJsonModel
                    {
                        name = "payment-info",
                        html = RenderPartialViewToString("OpcPaymentInfo", paymenInfoModel)
                    }
                });
            }
            catch (Exception exc)
            {
                _logger.Warning(exc.Message, exc, _workContext.CurrentCustomer);
                return Json(new { error = 1, message = exc.Message });
            }
        }

        [IgnoreAntiforgeryToken]
        public virtual IActionResult OpcConfirmOrder()
        {
            try
            {
                //validation
                if (_orderSettings.CheckoutDisabled)
                    throw new Exception(_localizationService.GetResource("Checkout.Disabled"));

                var cart = _shoppingCartService.GetShoppingCart(_workContext.CurrentCustomer, ShoppingCartType.ShoppingCart, _storeContext.CurrentStore.Id);

                if (!cart.Any())
                    throw new Exception("Your cart is empty");

                if (!_orderSettings.OnePageCheckoutEnabled)
                    throw new Exception("One page checkout is disabled");

                if (_customerService.IsGuest(_workContext.CurrentCustomer) && !_orderSettings.AnonymousCheckoutAllowed)
                    throw new Exception("Anonymous checkout is not allowed");

                //prevent 2 orders being placed within an X seconds time frame
                if (!IsMinimumOrderPlacementIntervalValid(_workContext.CurrentCustomer))
                    throw new Exception(_localizationService.GetResource("Checkout.MinOrderPlacementInterval"));

                //place order
                var processPaymentRequest = HttpContext.Session.Get<ProcessPaymentRequest>("OrderPaymentInfo");
                if (processPaymentRequest == null)
                {
                    //Check whether payment workflow is required
                    if (_orderProcessingService.IsPaymentWorkflowRequired(cart))
                    {
                        throw new Exception("Payment information is not entered");
                    }

                    processPaymentRequest = new ProcessPaymentRequest();
                }
                _paymentService.GenerateOrderGuid(processPaymentRequest);
                processPaymentRequest.StoreId = _storeContext.CurrentStore.Id;
                processPaymentRequest.CustomerId = _workContext.CurrentCustomer.Id;
                processPaymentRequest.PaymentMethodSystemName = _genericAttributeService.GetAttribute<string>(_workContext.CurrentCustomer,
                    NopCustomerDefaults.SelectedPaymentMethodAttribute, _storeContext.CurrentStore.Id);
                HttpContext.Session.Set<ProcessPaymentRequest>("OrderPaymentInfo", processPaymentRequest);
                var placeOrderResult = _orderProcessingService.PlaceOrder(processPaymentRequest);
                if (placeOrderResult.Success)
                {
                    HttpContext.Session.Set<ProcessPaymentRequest>("OrderPaymentInfo", null);
                    var postProcessPaymentRequest = new PostProcessPaymentRequest
                    {
                        Order = placeOrderResult.PlacedOrder
                    };

                    var paymentMethod = _paymentPluginManager
                        .LoadPluginBySystemName(placeOrderResult.PlacedOrder.PaymentMethodSystemName, _workContext.CurrentCustomer, _storeContext.CurrentStore.Id);
                    if (paymentMethod == null)
                        //payment method could be null if order total is 0
                        //success
                        return Json(new { success = 1 });

                    if (paymentMethod.PaymentMethodType == PaymentMethodType.Redirection)
                    {
                        //Redirection will not work because it's AJAX request.
                        //That's why we don't process it here (we redirect a user to another page where he'll be redirected)

                        //redirect
                        return Json(new
                        {
                            redirect = $"{_webHelper.GetStoreLocation()}checkout/OpcCompleteRedirectionPayment"
                        });
                    }

                    _paymentService.PostProcessPayment(postProcessPaymentRequest);
                    //success
                    return Json(new { success = 1 });
                }

                //error
                var confirmOrderModel = new CheckoutConfirmModel();
                foreach (var error in placeOrderResult.Errors)
                    confirmOrderModel.Warnings.Add(error);

                return Json(new
                {
                    update_section = new UpdateSectionJsonModel
                    {
                        name = "confirm-order",
                        html = RenderPartialViewToString("OpcConfirmOrder", confirmOrderModel)
                    },
                    goto_section = "confirm_order"
                });
            }
            catch (Exception exc)
            {
                _logger.Warning(exc.Message, exc, _workContext.CurrentCustomer);
                return Json(new { error = 1, message = exc.Message });
            }
        }

        public virtual IActionResult OpcCompleteRedirectionPayment()
        {
            try
            {
                //validation
                if (!_orderSettings.OnePageCheckoutEnabled)
                    return RedirectToRoute("Homepage");

                if (_customerService.IsGuest(_workContext.CurrentCustomer) && !_orderSettings.AnonymousCheckoutAllowed)
                    return Challenge();

                //get the order
                var order = _orderService.SearchOrders(storeId: _storeContext.CurrentStore.Id,
                customerId: _workContext.CurrentCustomer.Id, pageSize: 1)
                    .FirstOrDefault();
                if (order == null)
                    return RedirectToRoute("Homepage");

                var paymentMethod = _paymentPluginManager
                    .LoadPluginBySystemName(order.PaymentMethodSystemName, _workContext.CurrentCustomer, _storeContext.CurrentStore.Id);
                if (paymentMethod == null)
                    return RedirectToRoute("Homepage");
                if (paymentMethod.PaymentMethodType != PaymentMethodType.Redirection)
                    return RedirectToRoute("Homepage");

                //ensure that order has been just placed
                if ((DateTime.UtcNow - order.CreatedOnUtc).TotalMinutes > 3)
                    return RedirectToRoute("Homepage");

                //Redirection will not work on one page checkout page because it's AJAX request.
                //That's why we process it here
                var postProcessPaymentRequest = new PostProcessPaymentRequest
                {
                    Order = order
                };

                _paymentService.PostProcessPayment(postProcessPaymentRequest);

                if (_webHelper.IsRequestBeingRedirected || _webHelper.IsPostBeingDone)
                {
                    //redirection or POST has been done in PostProcessPayment
                    return Content(_localizationService.GetResource("Checkout.RedirectMessage"));
                }

                //if no redirection has been done (to a third-party payment page)
                //theoretically it's not possible
                return RedirectToRoute("CheckoutCompleted", new { orderId = order.Id });
            }
            catch (Exception exc)
            {
                _logger.Warning(exc.Message, exc, _workContext.CurrentCustomer);
                return Content(exc.Message);
            }
        }

        #endregion
    }
}
