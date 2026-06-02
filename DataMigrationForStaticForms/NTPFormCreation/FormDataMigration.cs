using Abp.Domain.Entities;
using Abp.Domain.Entities.Auditing;
using Core365.DoorStep;
using Core365.DoorStep.CustomForm;
using Core365.EntityFrameworkCore;
using Core365.RenewableEnergy.Account;
using Core365.RenewableEnergy.CustomForm;
using Core365.RenewableEnergy.CustomForm.REAccountCustomData;
using DataMigrationForStaticForms.GlobalOptions;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using static System.Net.WebRequestMethods;

namespace DataMigrationForStaticForms.NTPFormCreation
{
    public class NTPDataMigrationFile
    {
        private readonly Core365DbContext _core365DbContext;
        private readonly IBooleanMappingProvider _booleanMappingProvider;
        private bool _isBooleanOptionMapInitialized = false;
        private static readonly Dictionary<CustomFormDataBindingEnum, BooleanOptionIds> _customBooleanIdMappings = new Dictionary<CustomFormDataBindingEnum, BooleanOptionIds>();

        public NTPDataMigrationFile(Core365DbContext core365DbContext, IBooleanMappingProvider booleanMappingProvider)
        {
            _core365DbContext = core365DbContext;
            _booleanMappingProvider = booleanMappingProvider;
        }

        public async Task InitializeBooleanOptionMappingsAsync(int tenantId, List<BooleanMapConfigDto> mappings)
        {
            if (_isBooleanOptionMapInitialized) return;

            var mappingsToLoad = mappings;

            try
            {
                var optionLookup = await _core365DbContext.CustomFormGlobalFieldOptions
                    .Where(x => x.IsActive && !x.IsDeleted && x.TenantId == tenantId)
                    .ToDictionaryAsync(
                        x => (x.CustomFormDataBinding, x.Name),
                        x => x.Id
                    );

                int? TryGetOptionId(CustomFormDataBindingEnum binding, string optionName)
                {
                    if (string.IsNullOrWhiteSpace(optionName)) return 0;
                    if (optionLookup.TryGetValue((binding, optionName), out int id))
                    {
                        return id;
                    }
                    Console.WriteLine($"WARNING: Global Field Option '{optionName}' for Binding '{binding}' was not found. This field data will be SKIPPED during migration.");
                    return null;
                }

                foreach (var config in mappingsToLoad)
                {
                    var trueId = TryGetOptionId(config.Binding, config.TrueOptionName);

                    int? falseId = config.HasExplicitFalse
                        ? TryGetOptionId(config.Binding, config.FalseOptionName)
                        : 0;


                    if ((trueId.HasValue && falseId.HasValue))
                    {
                        _customBooleanIdMappings[config.Binding] = new BooleanOptionIds
                        {
                            TrueOptionId = trueId.Value,
                            FalseOptionId = falseId.Value
                        };
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"CRITICAL ERROR during custom boolean ID initialization: {ex.Message}");
                throw;
            }

            _isBooleanOptionMapInitialized = true;
            Console.WriteLine($"Initialized custom boolean ID mappings for {_customBooleanIdMappings.Count} bindings (NTP Focused).");
        }

        public async Task ExecuteAsync(int tenantId, List<BooleanMapConfigDto> booleanMappingConfigDtos)
        {
            Console.WriteLine($"--- Starting NTP Data Migration for Tenant ID: {tenantId} ---");

            await InitializeBooleanOptionMappingsAsync(tenantId, booleanMappingConfigDtos);

            var formMapping = await _core365DbContext.DoorStepCustomFormIncludeWithMappings
                .AsNoTracking()
                .Where(x => x.TenantId == tenantId && !x.IsDeleted && x.IncludeWithId == CustomFormIncludesWithTypes.NTP)
                .FirstOrDefaultAsync();

            if (formMapping == null)
            {
                Console.WriteLine($"No NTP form mapping found for Tenant {tenantId} ");
                return;
            }

            var formId = formMapping.FormId;

            var formSections = await _core365DbContext.DoorStepCustomFormSections
                .AsNoTracking()
                .Where(x => x.TenantId == tenantId && !x.IsDeleted && x.FormId == formId)
                .Include(x => x.CustomFormSectionFields)
                .ToListAsync();

            if (formSections.Count == 0)
            {
                Console.WriteLine($"No NTP form sections found for tenant {tenantId}");
                return;
            }

            var sectionIds = formSections.Select(x => x.Id).ToList();

            var formFields = await _core365DbContext.DoorStepCustomFormSectionFields
                .AsNoTracking()
                .Where(x => x.TenantId == tenantId && !x.IsDeleted && x.SectionId.HasValue && sectionIds.Contains(x.SectionId.Value))
                .Include(x => x.DoorStepCustomFormCustomControlTypeValues)
                .ThenInclude(x => x.DoorStepCustomFormSectionConditionalFields)
                .ThenInclude(x => x.DoorStepCustomFormSectionConditionalOptions)
                .ToListAsync();

            var ntpData = await _core365DbContext.RenewableEnergyAccountNtps
                .Where(x => x.TenantId == tenantId && !x.IsDeleted)
                .ToListAsync();

            await ProcessUtilityBillSectionAsync(formFields, formId, tenantId, ntpData);
            await ProcessHOASectionAsync(formFields, formId, tenantId, ntpData);
            await ProcessWelcomeCallSectionAsync(formFields, formId, tenantId, ntpData);

            await MigrateBindingDataAsync(tenantId);

            _isBooleanOptionMapInitialized = false;
            Console.WriteLine($"--- NTP Data Migration Complete for Tenant ID: {tenantId} ---");
        }

        private async Task ProcessUtilityBillSectionAsync(
            List<DoorStepCustomFormSectionField> formFields,
            int formId,
            int tenantId,
            List<RenewableEnergyAccountNtp> ntpData)
        {
            Console.WriteLine("Processing Utility Bill Section...");
            var customFormsFieldToInsert = new List<RenewableEnergyCustomForm>();
            var customFormsConditionalFieldsToInsert = new List<RenewableEnergyCustomFormConditionalValue>();
            var ntpMappings = _booleanMappingProvider.GetNTPMappings();

            foreach (var ntp in ntpData)
            {
                foreach (var mapping in _utilityBillMapping)
                {
                    var formField = formFields.FirstOrDefault(f => f.Title == mapping.Title);
                    if (formField == null) continue;

                    var ntpValue = ntp.GetType().GetProperty(mapping.PropertyName)?.GetValue(ntp);
                    if (ntpValue == null) continue;

                    // Handle boolean values with options
                    string valueToSave = ntpValue.ToString();
                    if (mapping.HasOptions && ntpValue is bool boolValue)
                    {
                        var optionMapping = ntpMappings.FirstOrDefault(m => m.Title == mapping.Title);
                        var optionName = boolValue ? optionMapping.TrueOptionName : optionMapping.FalseOptionName;
                        var optionId = formField.DoorStepCustomFormCustomControlTypeValues.FirstOrDefault(o => o.Value == optionName)?.Id;
                        if (optionId.HasValue)
                            valueToSave = optionId.Value.ToString();
                    }

                    customFormsFieldToInsert.Add(new RenewableEnergyCustomForm
                    {
                        TenantId = tenantId,
                        AccountId = ntp.AccountId,
                        FormId = formId,
                        CustomFormSectionFieldId = formField.Id,
                        FormFieldName = formField.Title,
                        Value = valueToSave,
                        CompletedDate = null,
                        IsCompleted = false,
                        CompletedByUserId = null,
                    });
                }

                // Handle conditional fields for Utility Bill
                var parentField = formFields.FirstOrDefault(x => x.Title == "Has utility bill been received?");
                if (parentField != null)
                {
                    var trueOption = parentField.DoorStepCustomFormCustomControlTypeValues
                        .FirstOrDefault(o => o.Value == ntpMappings.First(m => m.Title == "Has utility bill been received?").TrueOptionName);

                    if (trueOption != null && ntp.IsUtilityBillReceived == true)
                    {
                        var conditionalField = trueOption.DoorStepCustomFormSectionConditionalFields
                            .FirstOrDefault(x => x.Title == "Does the Customer information match?");

                        if (conditionalField != null)
                        {
                            string conditionalValue = ntp.IsCustomerInfoMatched.HasValue
                                ? (ntp.IsCustomerInfoMatched.Value
                                    ? conditionalField.DoorStepCustomFormSectionConditionalOptions
                                        .FirstOrDefault(o => o.Value == ntpMappings.First(m => m.Title == "Does the Customer information match?").TrueOptionName)?.Id.ToString()
                                    : conditionalField.DoorStepCustomFormSectionConditionalOptions
                                        .FirstOrDefault(o => o.Value == ntpMappings.First(m => m.Title == "Does the Customer information match?").FalseOptionName)?.Id.ToString())
                                : null;

                            if (!string.IsNullOrEmpty(conditionalValue))
                            {
                                customFormsConditionalFieldsToInsert.Add(new RenewableEnergyCustomFormConditionalValue
                                {
                                    TenantId = tenantId,
                                    AccountId = ntp.AccountId,
                                    FormId = formId,
                                    ParentId = parentField.Id,
                                    Value = conditionalValue,
                                    ConditionalFieldId = conditionalField.Id,
                                });
                            }
                        }
                    }
                }
            }
        
            if (customFormsFieldToInsert.Any())
            {
                await InsertUniqueFieldsDataAsync(customFormsFieldToInsert, tenantId);
            }
            if (customFormsConditionalFieldsToInsert.Any())
            {
                await InsertUniqueConditionalFieldsDataAsync(customFormsConditionalFieldsToInsert, tenantId);
            }
        }

        private async Task ProcessHOASectionAsync(
            List<DoorStepCustomFormSectionField> formFields,
            int formId,
            int tenantId,
            List<RenewableEnergyAccountNtp> ntpData)
        {
            Console.WriteLine("Processing HOA Section...");
            var customFormsFieldToInsert = new List<RenewableEnergyCustomForm>();
            var customFormsConditionalFieldsToInsert = new List<RenewableEnergyCustomFormConditionalValue>();
            var ntpMappings = _booleanMappingProvider.GetNTPMappings();

            foreach (var ntp in ntpData)
            {
                foreach (var mapping in _hoaMapping)
                {
                    var formField = formFields.FirstOrDefault(f => f.Title == mapping.Title);
                    if (formField == null) continue;

                    var ntpValue = ntp.GetType().GetProperty(mapping.PropertyName)?.GetValue(ntp);
                    if (ntpValue == null) continue;

                    // Handle boolean values with options
                    string valueToSave = ntpValue.ToString();
                    if (mapping.HasOptions && ntpValue is bool boolValue)
                    {
                        var optionMapping = ntpMappings.FirstOrDefault(m => m.Title == mapping.Title);
                        var optionName = boolValue ? optionMapping.TrueOptionName : optionMapping.FalseOptionName;
                        var optionId = formField.DoorStepCustomFormCustomControlTypeValues.FirstOrDefault(o => o.Value == optionName)?.Id;
                        if (optionId.HasValue)
                            valueToSave = optionId.Value.ToString();
                    }

                    customFormsFieldToInsert.Add(new RenewableEnergyCustomForm
                    {
                        TenantId = tenantId,
                        AccountId = ntp.AccountId,
                        FormId = formId,
                        CustomFormSectionFieldId = formField.Id,
                        FormFieldName = formField.Title,
                        Value = valueToSave,
                        CompletedDate = null,
                        IsCompleted = false,
                        CompletedByUserId = null,
                    });
                }

                // Handle conditional fields for HOA
                var parentField = formFields.FirstOrDefault(x => x.Title == "Does the customer have an HOA?");
                if (parentField != null && ntp.IsHoaPresent == true)
                {
                    var trueOption = parentField.DoorStepCustomFormCustomControlTypeValues
                        .FirstOrDefault(o => o.Value == ntpMappings.First(m => m.Title == "Does the customer have an HOA?").TrueOptionName);

                    if (trueOption != null)
                    {
                        var conditionalFields = new[] { "Name", "Phone", "Email", "HOA Address" };
                        foreach (var fieldTitle in conditionalFields)
                        {
                            var conditionalField = trueOption.DoorStepCustomFormSectionConditionalFields
                                .FirstOrDefault(x => x.Title == fieldTitle);

                            if (conditionalField != null)
                            {
                                string conditionalValue = fieldTitle switch
                                {
                                    "Name" => ntp.HoaName,
                                    "Phone" => ntp.HoaPhone,
                                    "Email" => ntp.HoaEmail,
                                    "HOA Address" => JsonSerializer.Serialize(new
                                    {
                                        Address1 = ntp.HoaAddress,
                                        AptOrSuite = ntp.HoaAptSuite,
                                        City = ntp.HoaCity,
                                        StateCode = ntp.HoaState,
                                        PostalCode = ntp.HoaZip
                                    }),
                                    _ => null
                                };

                                if (!string.IsNullOrEmpty(conditionalValue))
                                {
                                    customFormsConditionalFieldsToInsert.Add(new RenewableEnergyCustomFormConditionalValue
                                    {
                                        TenantId = tenantId,
                                        AccountId = ntp.AccountId,
                                        FormId = formId,
                                        ParentId = parentField.Id,
                                        Value = conditionalValue,
                                        ConditionalFieldId = conditionalField.Id,
                                    });
                                }
                            }
                        }
                    }
                }
            }

            if (customFormsFieldToInsert.Any())
            {
                await InsertUniqueFieldsDataAsync(customFormsFieldToInsert, tenantId);
            }
            if (customFormsConditionalFieldsToInsert.Any())
            {
                await InsertUniqueConditionalFieldsDataAsync(customFormsConditionalFieldsToInsert, tenantId);
            }
        }

        private async Task ProcessWelcomeCallSectionAsync(
            List<DoorStepCustomFormSectionField> formFields,
            int formId,
            int tenantId,
            List<RenewableEnergyAccountNtp> ntpData)
        {
            Console.WriteLine("Processing Welcome Call Section...");
            var customFormsFieldToInsert = new List<RenewableEnergyCustomForm>();
            var customFormsConditionalFieldsToInsert = new List<RenewableEnergyCustomFormConditionalValue>();
            var ntpMappings = _booleanMappingProvider.GetNTPMappings();

            foreach (var ntp in ntpData)
            {
                foreach (var mapping in _welcomeCallMapping)
                {
                    // Skip empty titles (used for grouping)
                    if (string.IsNullOrEmpty(mapping.Title)) continue;

                    var formField = formFields.FirstOrDefault(f => f.Title == mapping.Title);
                    if (formField == null) continue;

                    var ntpValue = ntp.GetType().GetProperty(mapping.PropertyName)?.GetValue(ntp);
                    if (ntpValue == null) continue;

                    // Handle boolean values with options
                    string valueToSave = ntpValue.ToString();
                    if (mapping.HasOptions && ntpValue is bool boolValue)
                    {
                        var optionMapping = ntpMappings.FirstOrDefault(m => m.Title == mapping.Title);
                        if (optionMapping != null)
                        {
                            var optionName = boolValue ? optionMapping.TrueOptionName : optionMapping.FalseOptionName;
                            var optionId = formField.DoorStepCustomFormCustomControlTypeValues.FirstOrDefault(o => o.Value == optionName)?.Id;
                            if (optionId.HasValue)
                                valueToSave = optionId.Value.ToString();
                        }
                    }

                    customFormsFieldToInsert.Add(new RenewableEnergyCustomForm
                    {
                        TenantId = tenantId,
                        AccountId = ntp.AccountId,
                        FormId = formId,
                        CustomFormSectionFieldId = formField.Id,
                        FormFieldName = formField.Title,
                        Value = valueToSave,
                        CompletedDate = null,
                        IsCompleted = false,
                        CompletedByUserId = null,
                    });
                }

                // Handle conditional fields for Welcome Call
                var parentField = formFields.FirstOrDefault(x => x.Title == "Was Welcome Call completed?");
                //if (parentField != null && ntp.IsWelcomeCallPresent == true)
                //{
                //    var trueOption = parentField.DoorStepCustomFormCustomControlTypeValues
                //        .FirstOrDefault(o => o.Value == ntpMappings.First(m => m.Title == "Was Welcome Call completed?").TrueOptionName);

                //    if (trueOption != null)
                //    {
                //        var conditionalFields = new[] { "Date Sales Rep Notified by Email", "Date Sales Rep Notified by SMS", "Date Customer Notified by Email", "Date Customer Notified by SMS" };
                //        foreach (var fieldTitle in conditionalFields)
                //        {
                //            var conditionalField = trueOption.DoorStepCustomFormSectionConditionalFields
                //                .FirstOrDefault(x => x.Title == fieldTitle);

                //            if (conditionalField != null)
                //            {
                //                string conditionalValue = fieldTitle switch
                //                {
                //                    "Date Sales Rep Notified by Email" => JsonSerializer.Serialize(new
                //                    {
                //                        SentDate = ntp.WelcomeCallNotifiedSalesRepEmailDate,
                //                        IsSent = ntp.WelcomeCallNotifiedSalesRepEmailDate.HasValue
                //                    }),

                //                    "Date Sales Rep Notified by SMS" => JsonSerializer.Serialize(new
                //                    {
                //                        SentDate = ntp.WelcomeCallNotifiedSalesRepSmsDate,
                //                        IsSent = ntp.WelcomeCallNotifiedSalesRepSmsDate.HasValue
                //                    }),

                //                    "Date Customer Notified by Email" => JsonSerializer.Serialize(new
                //                    {
                //                        SentDate = ntp.WelcomeCallNotifiedCustomerEmailDate,
                //                        IsSent = ntp.WelcomeCallNotifiedCustomerEmailDate.HasValue
                //                    }),

                //                    "Date Customer Notified by SMS" => JsonSerializer.Serialize(new
                //                    {
                //                        SentDate = ntp.WelcomeCallNotifiedCustomerSmsDate,
                //                        IsSent = ntp.WelcomeCallNotifiedCustomerSmsDate.HasValue
                //                    }),

                //                    _ => null
                //                };

                //                if (!string.IsNullOrEmpty(conditionalValue))
                //                {
                //                    customFormsConditionalFieldsToInsert.Add(new RenewableEnergyCustomFormConditionalValue
                //                    {
                //                        TenantId = tenantId,
                //                        AccountId = ntp.AccountId,
                //                        FormId = formId,
                //                        ParentId = parentField.Id,
                //                        Value = conditionalValue,
                //                        ConditionalFieldId = conditionalField.Id,
                //                    });
                //                }
                //            }
                //        }
                //    }
                //}
            }

            if (customFormsFieldToInsert.Any())
            {
                await InsertUniqueFieldsDataAsync(customFormsFieldToInsert, tenantId);
            }
            if (customFormsConditionalFieldsToInsert.Any())
            {
                await InsertUniqueConditionalFieldsDataAsync(customFormsConditionalFieldsToInsert, tenantId);
            }
        }

        public async Task MigrateBindingDataAsync(int tenantId)
        {
            Console.WriteLine($"Starting NTP binding data migration for Tenant {tenantId}...");

            var sourceData = await _core365DbContext.RenewableEnergyAccountNtps
                .AsNoTracking()
                .Where(p => p.TenantId == tenantId && p.IsDeleted == false)
                .ToListAsync();

            var newCustomData = new List<RenewableEnergyAccountCustomData>();

            foreach (var item in sourceData)
            {
                // Migrate standard bindings
                PivotAndAddData(newCustomData, item, _databindingNTPMapping, item.AccountId, item.CreationTime);

            }

            await InsertUniqueDataAsync(newCustomData, tenantId);
            Console.WriteLine($"Completed NTP binding data migration for Tenant ID: {tenantId}. Total KVP records: {newCustomData.Count}");
        }

        private async Task InsertUniqueFieldsDataAsync(List<RenewableEnergyCustomForm> newCustomData, int tenantId)
        {
            var bindingsToSkip = new HashSet<(int AccountId, int FiledId)>();

            var newFields = newCustomData.Select(d => d.CustomFormSectionFieldId).Distinct().ToList();

            if (!newFields.Any())
            {
                Console.WriteLine("No data to process for insertion.");
                return;
            }

            var existingData = await _core365DbContext.RenewableEnergyCustomForms
                .AsNoTracking()
                .Where(cd => newFields.Contains(cd.CustomFormSectionFieldId) && cd.IsDeleted == false)
                .Where(x => x.TenantId == tenantId)
                .Select(cd => new { cd.AccountId, cd.CustomFormSectionFieldId })
                .ToListAsync();

            foreach (var existingItem in existingData)
            {
                bindingsToSkip.Add((existingItem.AccountId, existingItem.CustomFormSectionFieldId));
            }

            var uniqueDataToInsert = newCustomData
                .Where(d => !bindingsToSkip.Contains((d.AccountId, d.CustomFormSectionFieldId)))
                .ToList();

            if (uniqueDataToInsert.Any())
            {
                _core365DbContext.RenewableEnergyCustomForms.AddRange(uniqueDataToInsert);
                await _core365DbContext.SaveChangesAsync();
            }
        }

        private async Task InsertUniqueConditionalFieldsDataAsync(List<RenewableEnergyCustomFormConditionalValue> newCustomData, int tenantId)
        {
            var bindingsToSkip = new HashSet<(int AccountId, int FiledId)>();

            var newFields = newCustomData.Select(d => d.ConditionalFieldId).Distinct().ToList();

            if (!newFields.Any()) return;

            var existingData = await _core365DbContext.RenewableEnergyCustomFormConditionalValues
                .AsNoTracking()
                .Where(cd => newFields.Contains(cd.ConditionalFieldId) && cd.IsDeleted == false)
                .Where(x => x.TenantId == tenantId)
                .Select(cd => new { cd.AccountId, cd.ConditionalFieldId })
                .ToListAsync();

            foreach (var existingItem in existingData)
            {
                bindingsToSkip.Add((existingItem.AccountId, existingItem.ConditionalFieldId));
            }

            var uniqueDataToInsert = newCustomData
                .Where(d => !bindingsToSkip.Contains((d.AccountId, d.ConditionalFieldId)))
                .ToList();

            if (uniqueDataToInsert.Any())
            {
                _core365DbContext.RenewableEnergyCustomFormConditionalValues.AddRange(uniqueDataToInsert);
                await _core365DbContext.SaveChangesAsync();
            }
        }


        private static readonly List<NTPMigrationMapItemDto> _utilityBillMapping = new List<NTPMigrationMapItemDto>
        {
            new NTPMigrationMapItemDto(nameof(RenewableEnergyAccountNtp.IsUtilityBillReceived), "Has utility bill been received?", 1, true, ""),
            new NTPMigrationMapItemDto(nameof(RenewableEnergyAccountNtp.IsCustomerInfoMatched), "Does the Customer information match?", 2, true, "Has utility bill been received?"),
        };

        private static readonly List<NTPMigrationMapItemDto> _hoaMapping = new List<NTPMigrationMapItemDto>
        {
            new NTPMigrationMapItemDto(nameof(RenewableEnergyAccountNtp.IsHoaPresent), "Does the customer have an HOA?", 1, true, ""),
            new NTPMigrationMapItemDto(nameof(RenewableEnergyAccountNtp.HoaName), "Name", 2, false, "Does the customer have an HOA?"),
            new NTPMigrationMapItemDto(nameof(RenewableEnergyAccountNtp.HoaPhone), "Phone", 3, false, "Does the customer have an HOA?"),
            new NTPMigrationMapItemDto(nameof(RenewableEnergyAccountNtp.HoaEmail), "Email", 4, false, "Does the customer have an HOA?"),
        };

        private static readonly List<NTPMigrationMapItemDto> _welcomeCallMapping = new List<NTPMigrationMapItemDto>
        {
            new NTPMigrationMapItemDto(nameof(RenewableEnergyAccountNtp.IsWelcomeCallPresent), "Was the Welcome Call successfully completed with Callpilot?", 1, true, ""),
        };

        // Data Binding Mappings
        private static readonly List<MigrationMapItemDto> _databindingNTPMapping = new List<MigrationMapItemDto>
        {
            //    new MigrationMapItemDto(nameof(RenewableEnergyAccountNtp.WelcomeCallNotifiedSalesRepEmailDate), CustomFormDataBindingEnum.SendToSalesRepEmail),
            //    new MigrationMapItemDto(nameof(RenewableEnergyAccountNtp.WelcomeCallNotifiedSalesRepSmsDate), CustomFormDataBindingEnum.SendToSalesRepSMS),
            //    new MigrationMapItemDto(nameof(RenewableEnergyAccountNtp.WelcomeCallNotifiedCustomerEmailDate), CustomFormDataBindingEnum.SendToHomeownerEmail),
            //    new MigrationMapItemDto(nameof(RenewableEnergyAccountNtp.WelcomeCallNotifiedCustomerSmsDate), CustomFormDataBindingEnum.SendToHomeownerSMS),
        };


        private void PivotAndAddData<TSource>(
            List<RenewableEnergyAccountCustomData> targetList,
            TSource sourceItem,
            List<MigrationMapItemDto> map,
            int accountId,
            DateTime creationTime) where TSource : class
        {
            var sourceType = typeof(TSource);

            // Extract auditing data up-front using your interfaces
            int tenantId = (sourceItem as IMustHaveTenant)?.TenantId ?? default(int);
            long? creatorUserId = (sourceItem as ICreationAudited)?.CreatorUserId;

            foreach (var mapItem in map)
            {
                var propInfo = sourceType.GetProperty(mapItem.PropertyName);
                if (propInfo == null)
                {
                    Console.WriteLine($"WARNING: Property '{mapItem.PropertyName}' not found on entity '{sourceType.Name}'. Skipping binding {mapItem.Binding}.");
                    continue;
                }

                var value = propInfo.GetValue(sourceItem);
                string stringValue;

                // Check if the property has a valid DateTime value
                if (value is DateTime dateTimeValue)
                {
                    stringValue = JsonSerializer.Serialize(new
                    {
                        SentDate = (DateTime?)dateTimeValue,
                        IsSent = true
                    });
                }
                else
                {
                    // If it's null (or not a DateTime), serialize empty/false values
                    stringValue = JsonSerializer.Serialize(new
                    {
                        SentDate = (DateTime?)null,
                        IsSent = false
                    });
                }

                // Add the record to the target list
                targetList.Add(new RenewableEnergyAccountCustomData
                {
                    TenantId = tenantId,
                    AccountId = accountId,
                    CustomFormDataBinding = mapItem.Binding.Value,
                    Value = stringValue,
                    CreationTime = creationTime,
                    CreatorUserId = creatorUserId,
                    IsDeleted = false
                });
            }
        }

        //private void PivotAndAddData<TSource>(
        //    List<RenewableEnergyAccountCustomData> targetList,
        //    TSource sourceItem,
        //    List<MigrationMapItemDto> map,
        //    int accountId,
        //    DateTime creationTime) where TSource : class
        //{
        //    var sourceType = typeof(TSource);

        //    foreach (var mapItem in map)
        //    {
        //        var propInfo = sourceType.GetProperty(mapItem.PropertyName);
        //        if (propInfo == null)
        //        {
        //            Console.WriteLine($"WARNING: Property '{mapItem.PropertyName}' not found on entity '{sourceType.Name}'. Skipping binding {mapItem.Binding}.");
        //            continue;
        //        }

        //        var value = propInfo.GetValue(sourceItem);

        //        if (value != null)
        //        {
        //            var auditingItem = new
        //            {
        //                TenantId = (sourceItem as IMustHaveTenant)?.TenantId ?? default(int),
        //                CreatorUserId = (sourceItem as ICreationAudited)?.CreatorUserId,
        //                AccountId = accountId
        //            };

        //            AddCustomData(targetList, auditingItem, mapItem.Binding.Value, value, creationTime);
        //        }
        //    }
        //}

        //private void AddCustomData(List<RenewableEnergyAccountCustomData> targetList, object sourceItem, CustomFormDataBindingEnum binding, object value, DateTime creationTime)
        //{
        //    string stringValue = "";
        //    switch(binding)
        //    {
        //        case CustomFormDataBindingEnum.SendToHomeownerSMS:
        //        case CustomFormDataBindingEnum.SendToHomeownerEmail:
        //        case CustomFormDataBindingEnum.SendToSalesRepSMS:
        //        case CustomFormDataBindingEnum.SendToSalesRepEmail:

        //            break;
        //    }


        //    stringValue = binding switch
        //    {
        //        CustomFormDataBindingEnum.SendToHomeownerSMS => JsonSerializer.Serialize(new
        //        {
        //            SentDate = value,
        //            IsSent = 
        //        }),

        //        CustomFormDataBindingEnum.SendToHomeownerSMS => JsonSerializer.Serialize(new
        //        {
        //            SentDate = ntp.WelcomeCallNotifiedSalesRepSmsDate,
        //            IsSent = ntp.WelcomeCallNotifiedSalesRepSmsDate.HasValue
        //        }),

        //        CustomFormDataBindingEnum.SendToHomeownerSMS => JsonSerializer.Serialize(new
        //        {
        //            SentDate = ntp.WelcomeCallNotifiedCustomerEmailDate,
        //            IsSent = ntp.WelcomeCallNotifiedCustomerEmailDate.HasValue
        //        }),

        //        CustomFormDataBindingEnum.SendToHomeownerSMS => JsonSerializer.Serialize(new
        //        {
        //            SentDate = ntp.WelcomeCallNotifiedCustomerSmsDate,
        //            IsSent = ntp.WelcomeCallNotifiedCustomerSmsDate.HasValue
        //        }),

        //        _ => null
        //    };


        //    if (binding == CustomFormDataBindingEnum.SendToHomeownerSMS ||  bin)
        //    {
        //        if (value is bool booleanValue)
        //        {
        //            if (_customBooleanIdMappings.TryGetValue(binding, out var mapping))
        //            {
        //                stringValue = booleanValue ? mapping.TrueOptionId.ToString() : mapping.FalseOptionId.ToString();
        //            }
        //            else
        //            {
        //                stringValue = null;
        //            }
        //        }
        //    }
        //    else
        //    {
        //        stringValue = value.ToString();
        //    }

        //    if (string.IsNullOrEmpty(stringValue)) return;

        //    int tenantId = (int)sourceItem.GetType().GetProperty("TenantId").GetValue(sourceItem);
        //    long? creatorUserId = (long?)sourceItem.GetType().GetProperty("CreatorUserId").GetValue(sourceItem);
        //    int accountId = (int)sourceItem.GetType().GetProperty("AccountId").GetValue(sourceItem);

        //    var accountCustomData = new RenewableEnergyAccountCustomData
        //    {
        //        TenantId = tenantId,
        //        AccountId = accountId,
        //        CustomFormDataBinding = binding,
        //        Value = stringValue,
        //        CreationTime = creationTime,
        //        CreatorUserId = creatorUserId,
        //        IsDeleted = false
        //    };

        //    targetList.Add(accountCustomData);
        //}

        private async Task InsertUniqueDataAsync(List<RenewableEnergyAccountCustomData> newCustomData, int tenantId)
        {
            var bindingsToSkip = new HashSet<(int AccountId, CustomFormDataBindingEnum Binding)>();

            var newBindings = newCustomData.Select(d => d.CustomFormDataBinding).Distinct().ToList();

            if (!newBindings.Any())
            {
                Console.WriteLine("No custom data to process for insertion.");
                return;
            }

            var existingData = await _core365DbContext.RenewableEnergyAccountCustomData
                .AsNoTracking()
                .Where(cd => newBindings.Contains(cd.CustomFormDataBinding) && cd.IsDeleted == false)
                .Where(x => x.TenantId == tenantId)
                .Select(cd => new { cd.AccountId, cd.CustomFormDataBinding })
                .ToListAsync();

            foreach (var existingItem in existingData)
            {
                bindingsToSkip.Add((existingItem.AccountId, existingItem.CustomFormDataBinding));
            }

            var uniqueDataToInsert = newCustomData
                .Where(d => !bindingsToSkip.Contains((d.AccountId, d.CustomFormDataBinding)))
                .ToList();

            if (uniqueDataToInsert.Any())
            {
                _core365DbContext.RenewableEnergyAccountCustomData.AddRange(uniqueDataToInsert);
                await _core365DbContext.SaveChangesAsync();
            }
            else
            {
                Console.WriteLine("No new unique KVP records found to insert.");
            }
        }
    }

    public class NTPMigrationMapItemDto
    {
        public string PropertyName { get; set; }
        public string Title { get; set; }
        public int Order { get; set; }
        public bool HasOptions { get; set; }
        public string ParentFieldTitle { get; set; }

        public NTPMigrationMapItemDto(string propertyName, string title, int order, bool hasOptions, string parentFieldTitle)
        {
            PropertyName = propertyName;
            Title = title;
            Order = order;
            HasOptions = hasOptions;
            ParentFieldTitle = parentFieldTitle;
        }
    }

    public class BooleanOptionIds
    {
        public int TrueOptionId { get; set; }
        public int FalseOptionId { get; set; }
        public int InterConnectionMethod1Id { get; set; }
        public int InterConnectionMethod2Id { get; set; }
        public int InterConnectionMethod3Id { get; set; }
    }

    public class MigrationMapItemDto
    {
        public string PropertyName { get; }
        public CustomFormDataBindingEnum? Binding { get; }

        public MigrationMapItemDto(string propertyName, CustomFormDataBindingEnum binding)
        {
            PropertyName = propertyName;
            Binding = binding;
        }
    }
}

