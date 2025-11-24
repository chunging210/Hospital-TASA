using System.ComponentModel.DataAnnotations;

namespace TASA.Program.ModelState
{
    public class MinLengthI18n : MinLengthAttribute
    {
        private readonly string? _conditionalMethodName;

        public MinLengthI18n(int length) : base(length)
        {
            ErrorMessage = I18nMessgae.RequiredErrorMessage;
        }

        public MinLengthI18n(int length, string conditionalMethodName) : this(length)
        {
            _conditionalMethodName = conditionalMethodName;
        }

        protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
        {
            var isRequired = ConditionalMethod.IsRequired(_conditionalMethodName, validationContext);
            if (isRequired)
            {
                return base.IsValid(value, validationContext);
            }
            else
            {
                return ValidationResult.Success;
            }
        }
    }
}
