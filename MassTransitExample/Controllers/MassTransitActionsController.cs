using System;
using System.Threading.Tasks;
using MassTransit;
using MassTransitExample.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace MassTransitExample.Controllers
{
    /// <summary>
    ///  MassTransit
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    public class MassTransitActionsController : ControllerBase
    {
        private readonly ISendEndpointProvider _sendEndpointProvider;

        /// <inheritdoc />
        public MassTransitActionsController(ISendEndpointProvider sendEndpointProvider)
        {
            _sendEndpointProvider = sendEndpointProvider;
        }

        /// <summary>
        /// Create new queue message
        /// </summary>
        /// <returns></returns>
        [HttpGet("[action]")]
        public async Task<IActionResult> CreateQueueMessage()
        {
            try
            {
                await _sendEndpointProvider.Send(new TestModel
                {
                    Value = "Test"
                });

                return NoContent();
            }
            catch (Exception e)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, e.Message);
            }
        }
    }
}
