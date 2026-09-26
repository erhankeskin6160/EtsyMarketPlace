namespace EtsyMarketPlace.Application.Shipping;

using System;
using System.Collections.Generic;

/// <summary>
/// Paket ölçü doğrulamasının sonucu.
/// </summary>
public sealed class PackageMeasurementResult
{
    public bool IsValid { get; init; }
    public double WeightKg { get; init; }
    public double WidthCm { get; init; }
    public double LengthCm { get; init; }
    public double HeightCm { get; init; }
    public double Desi { get; init; }
    public double BillableWeightKg { get; init; }
    public bool HasWeightError { get; init; }
    public bool HasDimensionError { get; init; }
    public IReadOnlyList<string> Errors { get; init; } = Array.Empty<string>();
}

/// <summary>
/// Paket ölçü doğrulaması, desi ve faturalandırılacak ağırlık hesabı.
/// Arayüz katmanı bu hesabı kendi içinde yapmaz; sonucu buradan tüketir.
/// Sıfır/negatif/aşırı değerler sessizce varsayılana düşürülmez, açık hata döner.
/// </summary>
public static class PackageMeasurementCalculator
{
    public const double DesiDivisor = 5000.0;
    public const double MaxWeightKg = 150.0;
    public const double MaxDimensionCm = 300.0;

    public static double ComputeDesi(double widthCm, double lengthCm, double heightCm)
        => Math.Round((widthCm * lengthCm * heightCm) / DesiDivisor, 2);

    public static double ComputeBillableWeight(double weightKg, double desi)
        => Math.Max(weightKg, desi);

    public static PackageMeasurementResult Evaluate(double weightKg, double widthCm, double lengthCm, double heightCm)
    {
        var errors = new List<string>();
        bool weightError = false;
        bool dimensionError = false;

        if (weightKg <= 0)
        {
            errors.Add("Ağırlık 0'dan büyük olmalı.");
            weightError = true;
        }
        else if (weightKg > MaxWeightKg)
        {
            errors.Add($"Ağırlık {MaxWeightKg:0} kg'dan büyük olamaz.");
            weightError = true;
        }

        ValidateDimension(widthCm, "En", ref dimensionError, errors);
        ValidateDimension(lengthCm, "Boy", ref dimensionError, errors);
        ValidateDimension(heightCm, "Yükseklik", ref dimensionError, errors);

        bool isValid = !weightError && !dimensionError;
        double desi = isValid ? ComputeDesi(widthCm, lengthCm, heightCm) : 0;
        double billable = isValid ? ComputeBillableWeight(weightKg, desi) : 0;

        return new PackageMeasurementResult
        {
            IsValid = isValid,
            WeightKg = weightKg,
            WidthCm = widthCm,
            LengthCm = lengthCm,
            HeightCm = heightCm,
            Desi = desi,
            BillableWeightKg = billable,
            HasWeightError = weightError,
            HasDimensionError = dimensionError,
            Errors = errors
        };
    }

    private static void ValidateDimension(double value, string name, ref bool hasError, List<string> errors)
    {
        if (value <= 0)
        {
            errors.Add($"{name} 0'dan büyük olmalı.");
            hasError = true;
        }
        else if (value > MaxDimensionCm)
        {
            errors.Add($"{name} {MaxDimensionCm:0} cm'den büyük olamaz.");
            hasError = true;
        }
    }
}
