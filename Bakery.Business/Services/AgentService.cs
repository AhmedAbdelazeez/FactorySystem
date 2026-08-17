using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Bakery.DataAccess;
using Bakery.Domain.Entities;
using Bakery.Domain.Enums;
using Bakery.Business.DTOs;

namespace Bakery.Business.Services
{
    public interface IAgentService
    {
        Task<IEnumerable<AgentDto>> GetAllAgentsAsync(bool activeOnly = false);
        Task<AgentDto?> GetAgentByIdAsync(int id);
        Task<AgentDto> CreateAgentAsync(CreateAgentDto dto);
        Task<AgentDto> UpdateAgentAsync(CreateAgentDto dto);
        Task DeleteAgentAsync(int id);
        /// <summary>حساب الملخص التجميعي للوكيل (توتل البساكت، الفلوس، المدفوع، المتبقي)</summary>
        Task<AgentDto> GetAgentWithSummaryAsync(int agentId);
    }

    public class AgentService : IAgentService
    {
        private readonly BakeryDbContext _context;

        public AgentService(BakeryDbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<AgentDto>> GetAllAgentsAsync(bool activeOnly = false)
        {
            var query = _context.Agents.AsQueryable();
            if (activeOnly) query = query.Where(a => a.IsActive);

            var agents = await query.OrderBy(a => a.Name).ToListAsync();

            // حساب الملخص التجميعي من حركات الخزينة (مبيعات الإنتاج)
            var agentIds = agents.Select(a => a.Id).ToList();
            var saleTxs = await _context.TreasuryTransactions
                .Where(t => t.AgentId.HasValue
                         && agentIds.Contains(t.AgentId.Value)
                         && t.TransactionType == TreasuryTransactionType.Income
                         && t.Category == "مبيعات إنتاج")
                .GroupBy(t => t.AgentId!.Value)
                .Select(g => new
                {
                    AgentId = g.Key,
                    TotalBaskets = g.Sum(t => t.SoldBaskets ?? 0),
                    TotalAmount = g.Sum(t => t.Amount),
                    TotalPaid = g.Sum(t => t.PaidAmount),
                    TotalRemaining = g.Sum(t => t.RemainingAmount)
                })
                .ToListAsync();

            return agents.Select(a =>
            {
                var stats = saleTxs.FirstOrDefault(s => s.AgentId == a.Id);
                return new AgentDto
                {
                    Id = a.Id,
                    Name = a.Name,
                    Phone = a.Phone,
                    Notes = a.Notes,
                    IsActive = a.IsActive,
                    CreatedAt = a.CreatedAt,
                    TotalBasketsSold = stats?.TotalBaskets ?? 0,
                    TotalSalesAmount = stats?.TotalAmount ?? 0,
                    TotalPaidAmount = stats?.TotalPaid ?? 0,
                    TotalRemainingAmount = stats?.TotalRemaining ?? 0,
                };
            });
        }

        public async Task<AgentDto?> GetAgentByIdAsync(int id)
        {
            var a = await _context.Agents.FindAsync(id);
            if (a == null) return null;

            var stats = await _context.TreasuryTransactions
                .Where(t => t.AgentId == id
                         && t.TransactionType == TreasuryTransactionType.Income
                         && t.Category == "مبيعات إنتاج")
                .GroupBy(t => t.AgentId)
                .Select(g => new
                {
                    TotalBaskets = g.Sum(t => t.SoldBaskets ?? 0),
                    TotalAmount = g.Sum(t => t.Amount),
                    TotalPaid = g.Sum(t => t.PaidAmount),
                    TotalRemaining = g.Sum(t => t.RemainingAmount)
                })
                .FirstOrDefaultAsync();

            return new AgentDto
            {
                Id = a.Id,
                Name = a.Name,
                Phone = a.Phone,
                Notes = a.Notes,
                IsActive = a.IsActive,
                CreatedAt = a.CreatedAt,
                TotalBasketsSold = stats?.TotalBaskets ?? 0,
                TotalSalesAmount = stats?.TotalAmount ?? 0,
                TotalPaidAmount = stats?.TotalPaid ?? 0,
                TotalRemainingAmount = stats?.TotalRemaining ?? 0,
            };
        }

        public async Task<AgentDto> GetAgentWithSummaryAsync(int agentId)
        {
            return (await GetAgentByIdAsync(agentId))
                ?? throw new KeyNotFoundException("الوكيل غير موجود.");
        }

        public async Task<AgentDto> CreateAgentAsync(CreateAgentDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Name))
                throw new InvalidOperationException("اسم الوكيل مطلوب.");

            var agent = new Agent
            {
                Name = dto.Name.Trim(),
                Phone = dto.Phone?.Trim(),
                Notes = dto.Notes?.Trim(),
                IsActive = true,
                CreatedAt = DateTime.Now
            };

            _context.Agents.Add(agent);
            await _context.SaveChangesAsync();

            return (await GetAgentByIdAsync(agent.Id))!;
        }

        public async Task<AgentDto> UpdateAgentAsync(CreateAgentDto dto)
        {
            var agent = await _context.Agents.FindAsync(dto.Id)
                ?? throw new KeyNotFoundException("الوكيل غير موجود.");

            if (string.IsNullOrWhiteSpace(dto.Name))
                throw new InvalidOperationException("اسم الوكيل مطلوب.");

            agent.Name = dto.Name.Trim();
            agent.Phone = dto.Phone?.Trim();
            agent.Notes = dto.Notes?.Trim();

            _context.Agents.Update(agent);
            await _context.SaveChangesAsync();

            return (await GetAgentByIdAsync(agent.Id))!;
        }

        public async Task DeleteAgentAsync(int id)
        {
            var agent = await _context.Agents.FindAsync(id)
                ?? throw new KeyNotFoundException("الوكيل غير موجود.");

            // فقط نحدد IsActive = false بدلاً من الحذف الفعلي إذا كان عنده حركات
            bool hasTx = await _context.TreasuryTransactions.AnyAsync(t => t.AgentId == id);
            if (hasTx)
            {
                agent.IsActive = false;
                _context.Agents.Update(agent);
            }
            else
            {
                _context.Agents.Remove(agent);
            }
            await _context.SaveChangesAsync();
        }
    }
}
