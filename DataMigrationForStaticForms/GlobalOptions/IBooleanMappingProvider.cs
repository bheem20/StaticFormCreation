using System;
using System.Collections.Generic;
using System.Text;

namespace DataMigrationForStaticForms.GlobalOptions
{
    public interface IBooleanMappingProvider
    {
        List<BooleanMappingConfigDtoWithNoBinding> GetNTPMappings();
        List<BooleanMapConfigDto> GetNTPMappingsWithDataBinding();
    }
}
