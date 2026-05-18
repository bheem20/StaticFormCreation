using Core365.DoorStep.CustomForm;

namespace DataMigrationForStaticForms.GlobalOptions
{
    public class BooleanMappingProvider : IBooleanMappingProvider
    {
        public BooleanMappingProvider()
        {
            
        }
        public List<BooleanMappingConfigDtoWithNoBinding> GetNTPMappings()
        {
            
            return new List<BooleanMappingConfigDtoWithNoBinding> 
            { 
                new BooleanMappingConfigDtoWithNoBinding { Title = "Has utility bill been received?", TrueOptionName = "UB Present" , FalseOptionName = "UB Not Received" , HasExplicitFalse = true }, 
                new BooleanMappingConfigDtoWithNoBinding { Title = "Does the Customer information match?", TrueOptionName = "UB Matches" , FalseOptionName = "UB Does Not Match" , HasExplicitFalse = true }, 
                new BooleanMappingConfigDtoWithNoBinding { Title = "Does the Customer have an HOA?", TrueOptionName = "Has HOA" , FalseOptionName = "No HOA" , HasExplicitFalse = true },
                new BooleanMappingConfigDtoWithNoBinding { Title = "Was the Welcome Call successfully completed with Callpilot?", TrueOptionName = "Yes" , FalseOptionName = "No" , HasExplicitFalse = true } ,
            };
        }

        public List<BooleanMapConfigDto> GetNTPMappingsWithDataBinding()
        {
            return new List<BooleanMapConfigDto>
            {
                new() { Binding = CustomFormDataBindingEnum.HasFinancingBeenApproved, TrueOptionName = "Financing Approved", FalseOptionName = "Financing Not Approved" },
            };

        }

    }
}
