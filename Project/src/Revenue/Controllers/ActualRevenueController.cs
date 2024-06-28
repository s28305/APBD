using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Project.Helpers;
using Project.Revenue.Services;

namespace Project.Revenue.Controllers;

[ApiController]
[Route("api/revenue/actual")]
public class ActualRevenueController(RevenueContext context, IRevenueService revenueService) : ControllerBase
{
    [Authorize]
    [HttpGet("total")]
    public ActionResult<double> GetTotalRevenue()
    {
        try
        {
            double totalRevenue = 0;

            totalRevenue += context.Contracts.Where(c => c.IsSigned).Sum(c => c.Price);

            return Ok(totalRevenue);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"An error occurred: {ex.Message}");
        }
    }
    
    [Authorize]
    [HttpGet("product/{softwareSystemId:int}")]
    public async Task<ActionResult<double>> GetRevenueForProduct(int softwareSystemId)
    {
        if (await revenueService.SoftwareExists(softwareSystemId))
        {
            return NotFound("Software system not found.");
        }
        
        try
        {
            double productRevenue = 0;

            productRevenue += context.Contracts
                .Where(c => c.SoftwareSystemId == softwareSystemId && c.IsSigned)
                .Sum(c => c.Price);

            return Ok(productRevenue);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"An error occurred: {ex.Message}");
        }
    }
    
    [Authorize]
    [HttpGet("total/{currency}")]
    public async Task<ActionResult<double>> GetTotalRevenue(string currency)
    {
        try
        {
            var exchangeRate = await revenueService.GetExchangeRate(currency.ToUpper());
            var totalRevenuePln = context.Contracts.Where(c => c.IsSigned).Sum(c => c.Price);
            var totalRevenue = totalRevenuePln * exchangeRate;

            return Ok(totalRevenue);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"An error occurred: {ex.Message}");
        }
    }
    
    [Authorize]
    [HttpGet("product/{softwareSystemId:int}/{currency}")]
    public async Task<ActionResult<double>> GetRevenueForProduct(int softwareSystemId, string currency)
    {
        if (await revenueService.SoftwareExists(softwareSystemId))
        {
            return NotFound("Software system not found.");
        }
        
        try
        {
            var exchangeRate = await revenueService.GetExchangeRate(currency.ToUpper());
            var productRevenuePln = context.Contracts
                .Where(c => c.SoftwareSystemId == softwareSystemId && c.IsSigned)
                .Sum(c => c.Price);
            var productRevenue = productRevenuePln * exchangeRate;

            return Ok(productRevenue);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"An error occurred: {ex.Message}");
        }
    }
}