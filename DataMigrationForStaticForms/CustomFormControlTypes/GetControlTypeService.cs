using Core365.EntityFrameworkCore;
using DataMigrationForStaticForms.MigratorCommonConsts;
using Microsoft.EntityFrameworkCore;

namespace DataMigrationForStaticForms.CustomFormControlTypes
{
    public class GetControlTypeService
    {
        private readonly Core365DbContext _core365DbContext;

        public GetControlTypeService(Core365DbContext core365DbContext)
        {
            _core365DbContext = core365DbContext;
        }

        public class ControlTypeIdsDto
        {
            public int DropdownId { get; set; }
            public int SelectButtonId { get; set; }
            public int TextMultiLineId { get; set; }
            public int CheckBoxId { get; set; }
            public int ButtonId { get; set; }
            public int RoofControlId { get; set; }
            public int ImageId { get; set; }
            public int TextSingleLineId { get; set; }
            public int PhoneId { get; set; }
            public int EmailId { get; set; }
            public int CurrencyId { get; set; }
            public int NumberId { get; set; }
            public int AddressId { get; set; }
            public int AdderControlId { get; set; }
            public int DateControlId { get; set; }
            public int SSNControlId { get; set; }

        }

        public async Task<ControlTypeIdsDto> GetControlTypeValueIds(int tenantId)
        {
            // Fetch all relevant control types for the current tenant or global context.
            var controlTypes = await _core365DbContext.DoorStepCustomFormCustomControlTypes
                .Select(t => new { t.Type, t.Id })
                .ToListAsync();

            var dto = new ControlTypeIdsDto
            {
                DropdownId = controlTypes.FirstOrDefault(t => t.Type == MigrationConsts.DoorStepCustomControlTypeStrings.Dropdown)?.Id ??
                             throw new InvalidOperationException("Critical control type 'Menu (Dropdown)' not found in the database."),

                SelectButtonId = controlTypes.FirstOrDefault(t => t.Type == MigrationConsts.DoorStepCustomControlTypeStrings.SelectButton)?.Id ??
                                 throw new InvalidOperationException("Critical control type 'Select Button' not found in the database."),

                TextMultiLineId = controlTypes.FirstOrDefault(t => t.Type == MigrationConsts.DoorStepCustomControlTypeStrings.TextMultiLine)?.Id ??
                                  throw new InvalidOperationException("Critical control type 'Text (Multi-Line)' not found in the database."),

                CheckBoxId = controlTypes.FirstOrDefault(t => t.Type == MigrationConsts.DoorStepCustomControlTypeStrings.Checkbox)?.Id ??
                             throw new InvalidOperationException("Critical control type 'Checkbox' not found in the database."),

                ButtonId = controlTypes.FirstOrDefault(t => t.Type == MigrationConsts.DoorStepCustomControlTypeStrings.Button)?.Id ??
                             throw new InvalidOperationException("Critical control type 'Button' not found in the database."),

                RoofControlId = controlTypes.FirstOrDefault(t => t.Type == MigrationConsts.DoorStepCustomControlTypeStrings.RoofControl)?.Id ??
                                throw new InvalidOperationException("Critical control type 'Roof Control' not found in the database."),

                ImageId = controlTypes.FirstOrDefault(t => t.Type == MigrationConsts.DoorStepCustomControlTypeStrings.Image)?.Id ??
                                throw new InvalidOperationException("Critical control type 'Image' not found in the database."),

                TextSingleLineId = controlTypes.FirstOrDefault(t => t.Type == MigrationConsts.DoorStepCustomControlTypeStrings.TextSingleLine)?.Id ??
                                  throw new InvalidOperationException("Critical control type 'Text (Single-Line)' not found in the database."),

                PhoneId = controlTypes.FirstOrDefault(t => t.Type == MigrationConsts.DoorStepCustomControlTypeStrings.Phone)?.Id ?? 
                            throw new InvalidOperationException("Critical control type 'Phone' not found in the database."),

                EmailId = controlTypes.FirstOrDefault(t => t.Type == MigrationConsts.DoorStepCustomControlTypeStrings.Email)?.Id ?? 
                            throw new InvalidOperationException("Critical control type 'Email' not found in the database."),

                CurrencyId = controlTypes.FirstOrDefault(t => t.Type == MigrationConsts.DoorStepCustomControlTypeStrings.Currency)?.Id ?? 
                            throw new InvalidOperationException("Critical control type 'Currency' not found in the database."),

                NumberId = controlTypes.FirstOrDefault(t => t.Type == MigrationConsts.DoorStepCustomControlTypeStrings.Number)?.Id ?? 
                            throw new InvalidOperationException("Critical control type 'Number' not found in the database."),

                AdderControlId = controlTypes.FirstOrDefault(t => t.Type == MigrationConsts.DoorStepCustomControlTypeStrings.AdderControl)?.Id ??
                                throw new InvalidOperationException("Critical control type 'Adder Control' not found in the database."),
                AddressId = controlTypes.FirstOrDefault(t => t.Type == MigrationConsts.DoorStepCustomControlTypeStrings.AddressControl)?.Id ??
                            throw new InvalidOperationException("Critical control type 'Address Control' not found in the database."),
                DateControlId = controlTypes.FirstOrDefault(t => t.Type == MigrationConsts.DoorStepCustomControlTypeStrings.Date)?.Id ??
                        throw new InvalidOperationException("Critical control type 'Date Control' not found in the database."),
                SSNControlId = controlTypes.FirstOrDefault(t => t.Type == MigrationConsts.DoorStepCustomControlTypeStrings.SSNInput)?.Id ??
                throw new InvalidOperationException("Critical control type 'SSN Control' not found in the database."),


            };

            return dto;
        }

    }
}
