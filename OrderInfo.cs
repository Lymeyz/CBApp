using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Policy;

namespace CBApp1
{
    public class OrderInfo : ITimeBound
    {
        [JsonConstructor]
        public OrderInfo( string order_id,
                          string product_id,
                          OrderConfiguration order_configuration,
                          string side,
                          string status,
                          string created_time,
                          string filled_size,
                          string fee,
                          string client_order_id )
        {
            Order_Id = order_id;
            ProductId = product_id;
            OrderConfiguration = order_configuration;
            Price = double.Parse( order_configuration.limit_limit_gtc.limit_price, new CultureInfo( "En-Us" ) );
            Size = double.Parse( order_configuration.limit_limit_gtc.base_size, new CultureInfo( "En-Us" ) );
            Side = side;
            Status = status;
            Time = DateTime.Parse( created_time );
            FilledSize = double.Parse( filled_size, new CultureInfo( "En-Us" ) );
            if( fee != null )
            {
                if( fee.Length != 0 )
                {
                    Fee = double.Parse( fee, new CultureInfo( "En-Us" ) );
                }
                else
                {
                    Fee = 0.0;
                }
            }
            else
            {
                Fee = 0.0;
            }
            ClientOrderId = client_order_id;
            
        }

        internal OrderInfo(FileOrderInfo fileOrder)
        {
            this.ProductId = fileOrder.ProductId;
            this.Side = fileOrder.Side;
            this.Price = fileOrder.Price;
            this.Size = fileOrder.Size;
            this.Status = fileOrder.Status;
            this.FilledSize = fileOrder.FilledSize;
            this.Time = fileOrder.Time;
            this.Order_Id = fileOrder.Order_Id;
            this.OrderConfiguration = fileOrder.OrderConfiguration;
            this.ClientOrderId = fileOrder.ClientOrderId;
            this.Fee = fileOrder.Fee;
            if( fileOrder.AssociatedId != null )
            {
                this.AssociatedId = fileOrder.AssociatedId;
            }
        }

        internal OrderInfo(OrderInfo orderInfo)
        {
            if( orderInfo.Order_Id != null )
            {
                Order_Id = orderInfo.Order_Id;
            }
            if( orderInfo.Side != null )
            {
                Side = orderInfo.Side;
            }
            if( orderInfo.Status != null )
            {
                Status = orderInfo.Status;
            }
            if( orderInfo.Time != null )
            {
                Time = orderInfo.Time;
            }
            if( orderInfo.ClientOrderId != null )
            {
                ClientOrderId = orderInfo.ClientOrderId;
            }

            Fee = orderInfo.Fee;
            FilledSize = orderInfo.FilledSize;

            Order_Id = orderInfo.Order_Id;
            OrderConfiguration = orderInfo.OrderConfiguration;
            Price = orderInfo.Price;
            Size = orderInfo.Size;
        }

        public string Id { get; }
        public string ProductId { get; set; }
        public string Side { get; }
        public double Price { get; set; }
        public double Size { get; set; }
        public string Status { get; set; }
        public double FilledSize { get; set; }

        // created at
        public DateTime Time { get; set; }
        public string Order_Id { get; }
        public OrderConfiguration OrderConfiguration { get; set; }
        public string ClientOrderId { get; }
        public double Fee { get; set; }
        public string AssociatedId { get; set; }
        public List<string> FillTradeIds { get; set; }

    }

    public class OrderErrorResponse
    {
        public OrderErrorResponse( string error,
                                  string message,
                                  string error_details,
                                  string preview_failure_reason, 
                                  string new_order_failure_reason)
        {
            Error = error;
            Message = message;
            Error_Details = error_details;
            Preview_Failure_Reason = preview_failure_reason;
            New_Order_Failure_Reason = new_order_failure_reason;
        }

        public string Error { get; }
        public string Message { get; }
        public string Error_Details { get; }
        public string Preview_Failure_Reason { get; }
        public string New_Order_Failure_Reason { get; }
    }

    public class OrderSuccessResponse
    {
        public OrderSuccessResponse( string order_id,
                                    string product_id,
                                    string side,
                                    string client_order_id )
        {
            Order_Id = order_id;
            Product_Id = product_id;
            Side = side;
            Client_Order_Id = client_order_id;
        }

        public string Order_Id { get; }
        public string Product_Id { get; }
        public string Side { get; }
        public string Client_Order_Id { get; }
    }

    public class OrderInfoResponse
    {
        [JsonConstructor]
        public OrderInfoResponse( bool success,
                                  string failure_reason,
                                  string order_id,
                                  OrderSuccessResponse success_response,
                                  OrderErrorResponse error_response,
                                  OrderConfiguration order_configuration,
                                  string side,
                                  string status,
                                  string created_time,
                                  string filled_size,
                                  string fee,
                                  string pending_cancel,
                                  string settled,
                                  string cancel_message,
                                  string number_of_fills,
                                  string client_order_id )
        {
            Order_Id = order_id;
            Success_Response = success_response;
            Error_Response = error_response;
            Order_Configuration = order_configuration;

            Success = success;

            if( success )
            {
                if( order_id != null )
                {
                    Order_Id = order_id;
                }
                if( success_response != null )
                {
                    Success_Response = success_response;
                }
            }
            else
            {
                if( failure_reason != null )
                {
                    Failure_Reason = failure_reason;
                }
                if( error_response != null )
                {
                    Error_Response = error_response;
                }
            }
        }
        public bool Success { get; set; }
        public string Failure_Reason { get; set; }
        public string Order_Id { get; }
        public OrderSuccessResponse Success_Response { get; set; }
        public OrderErrorResponse Error_Response { get; set; }
        public OrderConfiguration Order_Configuration { get; set; }
    }

    internal class FileOrderInfo
    {
        public FileOrderInfo( string ProductId,
                         string Side,
                         double Price,
                         double Size,
                         string Status,
                         double FilledSize,
                         DateTime Time,
                         string Order_Id,
                         OrderConfiguration OrderConfiguration,
                         string ClientOrderId,
                         double Fee ,
                         string AssociatedId,
                         string[] FillTradeIds )
        {
            this.ProductId = ProductId;
            this.Side = Side;
            this.Price = Price;
            this.Size = Size;
            this.Status = Status;
            this.FilledSize = FilledSize;
            this.Time = Time;
            this.Order_Id = Order_Id;
            this.OrderConfiguration = OrderConfiguration;
            this.ClientOrderId = ClientOrderId;
            this.Fee = Fee;
            if( AssociatedId != null )
            {
                this.AssociatedId = AssociatedId;
            }
            if( FillTradeIds != null )
            {
                this.FillTradeIds = FillTradeIds;
            }
        }

        public string ProductId { get; }
        public string Side { get; }
        public double Price { get; }
        public double Size { get; }
        public string Status { get; }
        public double FilledSize { get; }
        public DateTime Time { get; }
        public string Order_Id { get; }
        public OrderConfiguration OrderConfiguration { get; }
        public string ClientOrderId { get; }
        public double Fee { get; }
        public string AssociatedId { get; set; }
        public string[] FillTradeIds { get; set; }
    }

    public class OrderHolder
    {
        [JsonConstructor]
        public OrderHolder(OrderInfo order)
        {
            Order = order;
        }

        public OrderInfo Order { get; }
    }
}