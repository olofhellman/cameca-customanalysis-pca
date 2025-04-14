using System.Globalization;
using System.Windows.Controls;

namespace Cameca.CustomAnalysis.Pca;

internal class NumericValidationRule : ValidationRule
{
    public override ValidationResult Validate(object value, CultureInfo cultureInfo)
    {
        if (value is null)
        {
            return new ValidationResult(false, null);
        }
        else if (float.TryParse((string)value, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.CurrentCulture, out float _))
        {
            return ValidationResult.ValidResult;
        }
        else
        {
            return new ValidationResult(false, "Invalid format");
        }
    }
}
