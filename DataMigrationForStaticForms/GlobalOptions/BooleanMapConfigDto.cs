using Core365.DoorStep.CustomForm;
using System;
using System.Collections.Generic;
using System.Text;

namespace DataMigrationForStaticForms.GlobalOptions
{
    public class BooleanMapConfigDto
    {
        public CustomFormDataBindingEnum Binding { get; set; }
        public string TrueOptionName { get; set; }
        public string FalseOptionName { get; set; }
        public bool HasExplicitFalse { get; set; } = true;

    }

    public class BooleanMappingConfigDtoWithNoBinding
    {
        public string Title { get; set; }
        public string TrueOptionName { get; set; }
        public string FalseOptionName { get; set; }
        public bool HasExplicitFalse { get; set; } = true;
    }


}
