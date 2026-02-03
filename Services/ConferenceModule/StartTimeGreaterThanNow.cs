using System.ComponentModel.DataAnnotations;

namespace TASA.Services.ConferenceModule
{
    /// <summary>
    /// 開始時間必須大於等於現在時間
    /// </summary>
    public class StartTimeGreaterThanNowAttribute(string startNowPropertyName) : ValidationAttribute
    {
        public string StartNowPropertyName { get; set; } = startNowPropertyName;

        protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
        {
            var model = validationContext.ObjectInstance;
            if (model == null)
            {
                return ValidationResult.Success;
            }

            var startNowProperty = model.GetType().GetProperty(StartNowPropertyName);
            if (startNowProperty == null || startNowProperty.PropertyType != typeof(bool))
            {
                return ValidationResult.Success;
            }

            var startNowValue = startNowProperty.GetValue(model) as bool?;
            if (startNowValue == true)
            {
                return ValidationResult.Success;
            }

            if (value is not DateTime startTime)
            {
                return ValidationResult.Success;
            }

            if (startTime < DateTime.Now)
            {
                return new ValidationResult("會議開始時間必須大於等於現在時間。");
            }

            return ValidationResult.Success;
        }
    }
}
