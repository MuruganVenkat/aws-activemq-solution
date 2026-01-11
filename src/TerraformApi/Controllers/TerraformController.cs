using Microsoft.AspNetCore.Mvc;
using TerraformApi.Services;

namespace TerraformApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TerraformController : ControllerBase
{
    private readonly TerraformService _terraformService;

    public TerraformController(TerraformService terraformService)
    {
        _terraformService = terraformService;
    }

    [HttpPost("init/{environment}")]
    public IActionResult Init(string environment)
    {
        try
        {
            var output = _terraformService.Init(environment);
            return Ok(new { Message = "Init Successful", Output = output });
        }
        catch (Exception ex)
        {
            return BadRequest(new { Error = ex.Message });
        }
    }

    [HttpPost("plan/{environment}")]
    public IActionResult Plan(string environment)
    {
        try
        {
            var output = _terraformService.Plan(environment);
            return Ok(new { Message = "Plan Successful", Output = output });
        }
        catch (Exception ex)
        {
            return BadRequest(new { Error = ex.Message });
        }
    }

    [HttpPost("apply/{environment}")]
    public IActionResult Apply(string environment)
    {
        try
        {
            var output = _terraformService.Apply(environment);
            return Ok(new { Message = "Apply Successful", Output = output });
        }
        catch (Exception ex)
        {
            return BadRequest(new { Error = ex.Message });
        }
    }
}
