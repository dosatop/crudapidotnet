using System.Text;
using Microsoft.AspNetCore.Mvc;
using Stripe;

namespace CrudApi.Webhook;

[ApiController]
[Route("api/webhooks")]
public class WebhookController : ControllerBase
{
    private readonly IConfiguration _config;

    public WebhookController(IConfiguration config)
    {
        _config = config;
    }

    [HttpPost("stripe")]
    public async Task<IActionResult> StripeWebhook()
    {
        var endpointSecret = _config["Stripe:WebhookSecret"];
        var signatureHeader = Request.Headers["Stripe-Signature"].ToString();

        var json = HttpContext.Items["RawBody"]?.ToString();
        try
        {
            if (string.IsNullOrEmpty(signatureHeader))
            {
                return BadRequest("Missing Stripe-Signature header");
            }

            var stripeEvent = EventUtility.ConstructEvent(
                json,
                signatureHeader,
                endpointSecret,
                throwOnApiVersionMismatch: false
            );

            switch (stripeEvent.Type)
            {
                case "checkout.session.completed":
                    var session = stripeEvent.Data.Object as Stripe.Checkout.Session;
                    Console.WriteLine($"Payment successful: {session?.Id}");
                    break;

                case "payment_intent.succeeded":
                    var paymentIntent = stripeEvent.Data.Object as PaymentIntent;
                    Console.WriteLine($"PaymentIntent succeeded: {paymentIntent?.Id}");
                    break;
            }

            return Ok();
        }
        catch (StripeException e)
        {
            Console.WriteLine($"❌ {e.Message}");
            return BadRequest($"Webhook error: {e.Message}");
        }
    }
}