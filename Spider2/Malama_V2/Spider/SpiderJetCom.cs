using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HtmlAgilityPack;
using NCommon;
using NCommon.Http;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Text.RegularExpressions;


namespace Malama
{
    public class SpiderJetCom : SpiderBase
    {
        private logInfo loginfo;
        private float min_shipping;
        private float fee_shipping;
        public SpiderJetCom()
        {
            loginfo = new logInfo();
        }

        public override void ListItem(List<Product> products, string keyword, Site site, HttpResult httpResult)
        {
            string content = httpResult.Html; //获取HTML
            this.GetMinFreeShipping(site);

            try
            {
                HtmlHelper html = new HtmlHelper(content);
                HtmlNodeCollection htmlNodeCollection = html.GetNodesByXPath("/div[1]/div[2]/div[@class='product desktop not_mobile']");
                if (htmlNodeCollection == null) { return; }
                foreach (var htmlNode in htmlNodeCollection)
                {
                    //get the url of detail page
                    var a = htmlNode.FirstChild;
                    string href = a.Attributes["href"].Value;
                    href = Regex.Split(href, "/ref=", RegexOptions.IgnoreCase)[0];
                    href = "https://jet.com" + href;
                    

                    //get nodes under a that contain product's information
                    var NodeUnder_a = a.ChildNodes;

                    //product image url
                    var img_node = NodeUnder_a[0];
                    string img_url = img_node.Attributes["style"].Value;
                    Regex im = new Regex("url\\('(.*)'\\)");
                    Match m = im.Match(img_url);
                    if (!m.Success)
                    { continue; }
                    img_url = m.Groups[1].ToString();

                    //product's title
                    var title_node = NodeUnder_a[3];
                    if (title_node == null) { continue; }
                    string title = title_node.InnerText;

                    //prodcut's price 
                    var price_node = NodeUnder_a[4];
                    if (price_node == null) { continue; }
                    string price = html.GetNodeByXPath("./div/span[@class='open-sans-regular']", price_node).InnerText;
                    price = price.Replace("$", "").Trim();

                    Product product = new Product();
                    product.Name = title;
                    product.Url = href;
                    product.Img = img_url;
                    product.Price = Convert.ToSingle(price);                   
                    if (product.Price < 1 || string.IsNullOrWhiteSpace(product.Name) || string.IsNullOrWhiteSpace(product.Img) || string.IsNullOrWhiteSpace(product.Url)
                                || !ValidUrl(product.Url) || !ValidUrl(product.Img))
                    {
                        continue;
                    }

                    if(product.Price > min_shipping)
                    {
                        product.Shipping = 1;
                        product.ShippingFee = 0;
                    }
                    else
                    {
                        product.Shipping = 1;
                        product.ShippingFee = fee_shipping;
                    }

                    product.Created = GetTimeStamp();


                    this.GetDetail(product, site);

                    products.Add(product);
                }               
                
            }
            catch (Exception ex)
            {
                logInfo logInfo = new logInfo();
                logInfo.KeyInfo = site.SiteName + " " + keyword + " ListItem: " + ex.Message;
                logInfo.Location = this.GetType().Name + "." + System.Reflection.MethodBase.GetCurrentMethod().Name;
                Log4Helper.ErrorLog(logInfo);
            }
        }

        public override void GetDetailHtml(Product product, Site site)
        {
            loginfo.Location = this.GetType().Name + "." + System.Reflection.MethodBase.GetCurrentMethod().Name;
            loginfo.Url = product.Url;
            loginfo.KeyInfo = site.SiteName;

            HtmlHelper html = new HtmlHelper(product.Html);

            //get product detail nodes
            var product_node = html.GetNodeByXPath("//div[@class='products']/div[@class='product']/div[@class='desktop']");

            //get product description
            var description_nodes = html.GetNodesByXPath("./div[@class='bottom']/div[1]/div[1]/div[@class='description open-sans-regular']/*",product_node);
            string description = "";

            if (description_nodes.Count > 0)
            {
                foreach (var sellpoint_node in description_nodes)
                {
                    if (sellpoint_node.Equals(description_nodes.First()))
                    {
                        description += sellpoint_node.FirstChild.InnerText.Trim();
                    }
                    else
                    {
                        description += "|" + sellpoint_node.FirstChild.InnerText.Trim();
                    }
                }
            }

            //get sell point
            var sellpoint_nodes = html.GetNodesByXPath("./div[@class='bottom']/div[1]/div[1]/div[@class='bullets open-sans-regular']/div", product_node);
            string sellpoints = "";
            if(sellpoint_nodes.Count>0)
            {
                foreach(var sellpoint_node in sellpoint_nodes)
                {
                    if (sellpoint_node.Equals(sellpoint_nodes.First()))
                    {
                        sellpoints += sellpoint_node.FirstChild.InnerText.Trim();
                    }
                    else
                    {
                        sellpoints += "|" + sellpoint_node.FirstChild.InnerText.Trim();
                    }
                }
            }

            product.Description = description;
            product.SellPoint = sellpoints;

        }

        public void GetMinFreeShipping(Site site)
        {
            logInfo logInfo = new logInfo();
            logInfo.Location = this.GetType().Name + "." + System.Reflection.MethodBase.GetCurrentMethod().Name;

            //get the minimun purchase amount for free shipping from it's F&Q page 
            string url = "https://jet.com/help-center/shipping-and-returns";

            HttpItem httpItem = new HttpItem();
            httpItem.Cookie = string.Empty;
            httpItem.Method = "GET";
            httpItem.Header = null;
            httpItem.Accept = "text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,*/*;q=0.8";
            httpItem.UserAgent = "Mozilla/5.0 (Windows NT 6.1; WOW64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/43.0.2357.130 Safari/537.36";
            //httpItem.Timeout = 60000 * 5;
            httpItem.URL = url;

            HttpHelper httpHelper = new HttpHelper();

            HttpResult httpResult = null;
            try
            {
                httpResult = httpHelper.GetHtml(httpItem);             
                if (httpResult == null)
                {
                    logInfo.KeyInfo = "Remote HTML is null (timeout etc.)";
                    Log4Helper.WarningLog(logInfo);
                }
                else
                {
                    if (httpResult.StatusCode != System.Net.HttpStatusCode.OK)
                    {
                        logInfo.KeyInfo = "Remote HTML is null (timeout etc.)";
                        Log4Helper.WarningLog(logInfo);
                    }
                    else
                    {
                        string content = httpResult.Html; 
                        if (content.Length < 200) 
                        {
                            return;
                        }

                        HtmlHelper html = new HtmlHelper(content);
                        var fq_node = html.GetNodeByXPath("//div[@class='page_content']/h1");
                        var ans = html.GetNodesByXPath("following-sibling::p", fq_node);
                        
                        Regex minfreeshipping_pattern = new Regex(@"orders\sover\s\$(\d+)");
                        Regex shippingfee_pattern = new Regex(@"fixed\sshipping\sfee\sof\s\$(\d+(\.\d+)?)");
                        int match_min = 0;
                        int match_fee = 0;
                        foreach(HtmlNode a in ans)
                        {
                            if(minfreeshipping_pattern.Match(a.InnerText).Success)
                            {
                                min_shipping = Convert.ToSingle(minfreeshipping_pattern.Match(a.InnerText).Groups[1].Value);
                                match_min = 1;
                            }
                            if (shippingfee_pattern.Match(a.InnerText).Success)
                            {
                                fee_shipping = Convert.ToSingle(shippingfee_pattern.Match(a.InnerText).Groups[1].Value);
                                match_fee = 1;
                            }
                            if(match_min==1&&match_fee==1)
                            {
                                continue;
                            }
                        }
                        

                    }
                }
            }
            catch (Exception ex)
            {
                logInfo.KeyInfo = "Could not get remote HTML";
                Log4Helper.ErrorLog(logInfo);
            }
        }


    }

    
}
