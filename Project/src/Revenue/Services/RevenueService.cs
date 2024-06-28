using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json.Linq;
using Project.Helpers;

namespace Project.Revenue.Services;

public class RevenueService(RevenueContext context): IRevenueService
{
    public async Task<bool> SoftwareExists(int softwareSystemId)
    {
        var softwareSystem = await context.SoftwareSystems
            .FirstOrDefaultAsync(s => s.Id == softwareSystemId);

        return softwareSystem == null;
    }
    
    public async Task<double> GetExchangeRate(string targetCurrency)
    { 
        var requestUrl = $"https://api.frankfurter.app/latest?from=PLN&to={targetCurrency}"; 
        
        using var client = new HttpClient();
        var response = await client.GetAsync(requestUrl);
        response.EnsureSuccessStatusCode();
        var responseBody = await response.Content.ReadAsStringAsync();
        var data = JObject.Parse(responseBody);
        return data["rates"][targetCurrency].Value<double>();
    }
}