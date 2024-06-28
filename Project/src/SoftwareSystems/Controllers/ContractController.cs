using Microsoft.AspNetCore.Authorization;
using Project.SoftwareSystems.Models;

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Project.Helpers;
using Project.SoftwareSystems.DTOs;

namespace Project.SoftwareSystems.Controllers;

[Route("api/contracts")]
[ApiController]
public class ContractController(RevenueContext context) : ControllerBase
{
    // POST: api/contracts
    [Authorize]
    [HttpPost]
    public async Task<ActionResult<Contract>> CreateContract(AddContractDto contractDto)
    {
        var softwareSystem = await context.SoftwareSystems
            .FirstOrDefaultAsync(s => s.Id == contractDto.SoftwareSystemId && s.Version == contractDto.SoftwareVersion);

        if (softwareSystem == null)
        {
            return NotFound("Software system not found.");
        }
        
        var individualClient = await context.Individuals
            .FirstOrDefaultAsync(i => i.Id == contractDto.ClientId);

        var companyClient = await context.Companies
            .FirstOrDefaultAsync(c => c.Id == contractDto.ClientId);

        if (individualClient == null && companyClient == null)
        {
            return NotFound("Client not found.");
        }
        
        if (!ValidDuration(contractDto.StartDate, contractDto.EndDate))
        {
            return BadRequest("Contract duration must be between 3 and 30 days.");
        }

        if (await HasActiveContract(contractDto.SoftwareSystemId, contractDto.ClientId))
        {
            return BadRequest("Client already has an active contract for the software system.");
        }
        
        var contract = contractDto.Map(softwareSystem.UpfrontCost);
        var totalPrice = GetTotalPrice(contract);
        contract.Price = totalPrice;

        context.Contracts.Add(contract);
        await context.SaveChangesAsync();

        return Created("", new { contract.Id, Contract = contractDto, contract.Price });
    }

    private static bool ValidDuration(DateTime startDate, DateTime endDate)
    {
        var contractDuration = (endDate - startDate).Days;
        return contractDuration is >= 3 and <= 30 && startDate < endDate;
    }

    private async Task<bool> HasActiveContract(int softwareSystemId, int clientId)
    {
        return await context.Contracts
            .AnyAsync(c => c.SoftwareSystemId == softwareSystemId 
                           && c.ClientId == clientId 
                           && c.IsSigned 
                           && !c.IsCancelled);
    }


    private double GetTotalPrice(Contract contract)
    {
        // upfront cost with discounts
        var totalPrice = GetDiscount(contract.Price, contract.SoftwareSystemId, contract.ClientId);
        
        // support costs
        totalPrice += contract.SupportYears * 1000;

        return totalPrice;
    }

    private double GetDiscount(double totalPrice, int softwareSystemId, int clientId)
    {
        var availableDiscount = context.Discounts
            .Where(d => d.SoftwareSystemId == softwareSystemId
                        && d.StartDate <= DateTime.Today
                        && d.EndDate >= DateTime.Today)
            .OrderByDescending(d => d.Value)
            .FirstOrDefault();

        if (availableDiscount != null)
        {
            totalPrice *= 1 - availableDiscount.Value * 0.01;

        }
        
        if (IsReturning(clientId))
        {
            totalPrice *= 0.95; 
        }

        return totalPrice;
    }

    private bool IsReturning(int clientId)
    {
        return context.Contracts.Any(c => c.ClientId == clientId && (c.IsSigned || !c.IsCancelled));
    }


    // POST: api/contracts/payments
    [Authorize]
    [HttpPost("payments")]
    public async Task<ActionResult<Payment>> IssuePayment(AddPaymentDto paymentDto)
    {
        var contract = await context.Contracts
            .Include(c => c.Payments) 
            .FirstOrDefaultAsync(c => c.Id == paymentDto.ContractId);
        
        if (contract == null)
        {
            return NotFound($"Contract with id {paymentDto.ContractId} was not found.");
        }

        if (contract.IsCancelled)
        {
            return BadRequest("Contract was cancelled.");
        }
        
        if (contract.IsSigned)
        {
            return BadRequest("Contract was already signed.");
        }

        if (DateTime.UtcNow > contract.EndDate)
        { 
            contract.Cancel();
            return BadRequest("Contract payment period has expired.");
        }
        
        var remainingPrice = contract.Price - contract.Payments.Sum(p => p.Amount);

        if (paymentDto.Amount <= 0 || paymentDto.Amount > remainingPrice)
        {
            return BadRequest($"Payment amount should be between 0 and {remainingPrice}.");
        }

        var payment = paymentDto.Map();
        contract.Payments.Add(payment);
        await context.SaveChangesAsync();

        if (!(contract.Price <= contract.Payments.Sum(p => p.Amount)))
            return Created("", new
            {
                id = payment.Id,
                Payment = paymentDto
            });
        contract.IsSigned = true;
        context.Entry(contract).State = EntityState.Modified;
        await context.SaveChangesAsync();

        return Created("", new
        {
            id = payment.Id,
            Payment = paymentDto,
            Message = "Contract was fully paid."
        });


    }
}