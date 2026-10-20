namespace EtsyMarketPlace.Infrastructure.Persistence.Repositories;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using EtsyMarketPlace.Domain.Entities;
using EtsyMarketPlace.Domain.Orders;
using EtsyMarketPlace.Infrastructure.Database.Postgres;

/// <summary>
/// PostgreSQL EF Core tabanlı sipariş alıcı teslimat adresi ve kargo meta verileri deposu (Clean Architecture Persistence).
/// </summary>
public sealed class EfPostgreSqlOrderAddressRepository : IOrderAddressRepository
{
    private readonly EtsyMarketPlaceDbContext _context;
    private readonly ILogger<EfPostgreSqlOrderAddressRepository> _logger;

    public EfPostgreSqlOrderAddressRepository(
        EtsyMarketPlaceDbContext context,
        ILogger<EfPostgreSqlOrderAddressRepository> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<OrderAddressRecord?> GetByReceiptIdAsync(long receiptId, CancellationToken cancellationToken = default)
    {
        var entity = await _context.OrderAddresses
            .AsNoTracking()
            .FirstOrDefaultAsync(o => o.ReceiptId == receiptId, cancellationToken);

        return entity is null ? null : ToRecord(entity);
    }

    public async Task<IReadOnlyDictionary<long, OrderAddressRecord>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var entities = await _context.OrderAddresses
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var dict = new Dictionary<long, OrderAddressRecord>();
        foreach (var entity in entities)
        {
            dict[entity.ReceiptId] = ToRecord(entity);
        }

        return dict;
    }

    public async Task SaveAsync(OrderAddressRecord record, CancellationToken cancellationToken = default)
    {
        if (record == null || record.ReceiptId <= 0) return;

        var existing = await _context.OrderAddresses
            .FirstOrDefaultAsync(o => o.ReceiptId == record.ReceiptId, cancellationToken);

        if (existing is null)
        {
            _context.OrderAddresses.Add(new OrderAddress
            {
                ReceiptId = record.ReceiptId,
                BuyerName = record.BuyerName ?? string.Empty,
                BuyerEmail = record.BuyerEmail ?? string.Empty,
                Phone = record.Phone ?? string.Empty,
                StreetAddress = record.StreetAddress ?? string.Empty,
                SecondAddress = record.SecondAddress ?? string.Empty,
                City = record.City ?? string.Empty,
                State = record.State ?? string.Empty,
                PostalCode = record.PostalCode ?? string.Empty,
                CountryCode = record.CountryCode ?? string.Empty,
                CountryName = record.CountryName ?? string.Empty,
                Source = record.Source ?? string.Empty,
                UpdatedAtUtc = record.UpdatedAtUtc.ToUniversalTime(),
                IossNumber = record.IossNumber ?? string.Empty,
                ProductCostUsd = record.ProductCostUsd,
                ShippingCostUsd = record.ShippingCostUsd,
                WeightKg = record.WeightKg,
                WidthCm = record.WidthCm,
                LengthCm = record.LengthCm,
                HeightCm = record.HeightCm,
                GtipCode = record.GtipCode ?? string.Empty,
                SelectedCarrier = record.SelectedCarrier ?? string.Empty,
                CarrierServiceName = record.CarrierServiceName ?? string.Empty,
                TrackingCode = record.TrackingCode ?? string.Empty,
                LabelUrl = record.LabelUrl ?? string.Empty
            });
        }
        else
        {
            ApplyUpdate(existing, record);
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task SaveBatchAsync(IEnumerable<OrderAddressRecord> records, CancellationToken cancellationToken = default)
    {
        if (records == null) return;

        var list = records.Where(r => r != null && r.ReceiptId > 0).ToList();
        if (list.Count == 0) return;

        var receiptIds = list.Select(r => r.ReceiptId).Distinct().ToList();
        var existingEntities = await _context.OrderAddresses
            .Where(o => receiptIds.Contains(o.ReceiptId))
            .ToDictionaryAsync(o => o.ReceiptId, cancellationToken);

        foreach (var record in list)
        {
            if (existingEntities.TryGetValue(record.ReceiptId, out var existing))
            {
                ApplyUpdate(existing, record);
            }
            else
            {
                var newEntity = new OrderAddress
                {
                    ReceiptId = record.ReceiptId,
                    BuyerName = record.BuyerName ?? string.Empty,
                    BuyerEmail = record.BuyerEmail ?? string.Empty,
                    Phone = record.Phone ?? string.Empty,
                    StreetAddress = record.StreetAddress ?? string.Empty,
                    SecondAddress = record.SecondAddress ?? string.Empty,
                    City = record.City ?? string.Empty,
                    State = record.State ?? string.Empty,
                    PostalCode = record.PostalCode ?? string.Empty,
                    CountryCode = record.CountryCode ?? string.Empty,
                    CountryName = record.CountryName ?? string.Empty,
                    Source = record.Source ?? string.Empty,
                    UpdatedAtUtc = record.UpdatedAtUtc.ToUniversalTime(),
                    IossNumber = record.IossNumber ?? string.Empty,
                    ProductCostUsd = record.ProductCostUsd,
                    ShippingCostUsd = record.ShippingCostUsd,
                    WeightKg = record.WeightKg,
                    WidthCm = record.WidthCm,
                    LengthCm = record.LengthCm,
                    HeightCm = record.HeightCm,
                    GtipCode = record.GtipCode ?? string.Empty,
                    SelectedCarrier = record.SelectedCarrier ?? string.Empty,
                    CarrierServiceName = record.CarrierServiceName ?? string.Empty,
                    TrackingCode = record.TrackingCode ?? string.Empty,
                    LabelUrl = record.LabelUrl ?? string.Empty
                };
                _context.OrderAddresses.Add(newEntity);
                existingEntities[record.ReceiptId] = newEntity;
            }
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    private static void ApplyUpdate(OrderAddress existing, OrderAddressRecord record)
    {
        if (!string.IsNullOrWhiteSpace(record.BuyerName)) existing.BuyerName = record.BuyerName;
        if (!string.IsNullOrWhiteSpace(record.BuyerEmail)) existing.BuyerEmail = record.BuyerEmail;
        if (!string.IsNullOrWhiteSpace(record.Phone)) existing.Phone = record.Phone;
        if (!string.IsNullOrWhiteSpace(record.StreetAddress)) existing.StreetAddress = record.StreetAddress;
        if (!string.IsNullOrWhiteSpace(record.SecondAddress)) existing.SecondAddress = record.SecondAddress;
        if (!string.IsNullOrWhiteSpace(record.City)) existing.City = record.City;
        if (!string.IsNullOrWhiteSpace(record.State)) existing.State = record.State;
        if (!string.IsNullOrWhiteSpace(record.PostalCode)) existing.PostalCode = record.PostalCode;
        if (!string.IsNullOrWhiteSpace(record.CountryCode)) existing.CountryCode = record.CountryCode;
        if (!string.IsNullOrWhiteSpace(record.CountryName)) existing.CountryName = record.CountryName;
        if (!string.IsNullOrWhiteSpace(record.Source)) existing.Source = record.Source;
        existing.UpdatedAtUtc = record.UpdatedAtUtc.ToUniversalTime();

        if (!string.IsNullOrWhiteSpace(record.IossNumber)) existing.IossNumber = record.IossNumber;
        if (record.ProductCostUsd > 0) existing.ProductCostUsd = record.ProductCostUsd;
        if (record.ShippingCostUsd > 0) existing.ShippingCostUsd = record.ShippingCostUsd;
        if (record.WeightKg > 0) existing.WeightKg = record.WeightKg;
        if (record.WidthCm > 0) existing.WidthCm = record.WidthCm;
        if (record.LengthCm > 0) existing.LengthCm = record.LengthCm;
        if (record.HeightCm > 0) existing.HeightCm = record.HeightCm;
        if (!string.IsNullOrWhiteSpace(record.GtipCode)) existing.GtipCode = record.GtipCode;
        if (!string.IsNullOrWhiteSpace(record.SelectedCarrier)) existing.SelectedCarrier = record.SelectedCarrier;
        if (!string.IsNullOrWhiteSpace(record.CarrierServiceName)) existing.CarrierServiceName = record.CarrierServiceName;
        if (!string.IsNullOrWhiteSpace(record.TrackingCode)) existing.TrackingCode = record.TrackingCode;
        if (!string.IsNullOrWhiteSpace(record.LabelUrl)) existing.LabelUrl = record.LabelUrl;
    }

    private static OrderAddressRecord ToRecord(OrderAddress e)
    {
        return new OrderAddressRecord(
            e.ReceiptId,
            e.BuyerName,
            e.BuyerEmail,
            e.Phone,
            e.StreetAddress,
            e.SecondAddress,
            e.City,
            e.State,
            e.PostalCode,
            e.CountryCode,
            e.CountryName,
            e.Source,
            new DateTime(e.UpdatedAtUtc.Ticks, DateTimeKind.Utc))
        {
            IossNumber = e.IossNumber,
            ProductCostUsd = e.ProductCostUsd,
            ShippingCostUsd = e.ShippingCostUsd,
            WeightKg = e.WeightKg,
            WidthCm = e.WidthCm,
            LengthCm = e.LengthCm,
            HeightCm = e.HeightCm,
            GtipCode = e.GtipCode,
            SelectedCarrier = e.SelectedCarrier,
            CarrierServiceName = e.CarrierServiceName,
            TrackingCode = e.TrackingCode,
            LabelUrl = e.LabelUrl
        };
    }
}
