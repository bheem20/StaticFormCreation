using Core365.DoorStep;
using Core365.DoorStep.CustomForm;
using Core365.DoorStep.CustomFormGlobalControl;
using Core365.EntityFrameworkCore;
using Core365.ProjectForm;
using DataMigrationForStaticForms.GlobalOptions;
using DataMigrationForStaticForms.MigratorCommonConsts;
using Microsoft.EntityFrameworkCore;
using static DataMigrationForStaticForms.CustomFormControlTypes.GetControlTypeService;
using static DataMigrationForStaticForms.MigratorCommonConsts.MigrationConsts;

namespace DataMigrationForStaticForms.NTPFormCreation
{
    public class FormCreation 
    {
        private readonly Core365DbContext _dbContext;
        private readonly IBooleanMappingProvider _booleanMappingProvider;
        private readonly string _DoorStepCustomFormNtpTitle = "NTP Form";
        private readonly string _GroupTitle = "NTP CustomForms";

        public FormCreation(Core365DbContext dbContext, IBooleanMappingProvider booleanMappingProvider)
        {
            _dbContext = dbContext;
            _booleanMappingProvider = booleanMappingProvider;
        }


        private async Task<List<CustomFormGlobalFieldOptionDto>> GetCustomFormGlobalFieldOptionList(int tenantId)
        {
            var data = await _dbContext
                .CustomFormGlobalFieldOptions
                .Where(x => x.IsActive && x.TenantId == tenantId && x.IsDeleted == false)
                .ToListAsync();

            var customFormGlobalFieldOptionDtoList = data.Select(x => new CustomFormGlobalFieldOptionDto
            {
                Id = x.Id,
                Name = x.Name,
                Description = x.Description,
                CustomFormDataBinding = x.CustomFormDataBinding,
                IsActive = x.IsActive,
                IsDefault = x.IsDefault,
                IsEditable = true,
                Value = x.Id,
            }).OrderBy(x => x.Name).ToList();

            return customFormGlobalFieldOptionDtoList;
        }

        public async Task CreateForms(int tenantId, ControlTypeIdsDto controlTypeIds)
        {
            Console.WriteLine($"Creating forms for tenant ID: {tenantId}");

            List<BooleanMappingConfigDtoWithNoBinding> ntpMappings = _booleanMappingProvider.GetNTPMappings();

            List<BooleanMapConfigDto> ntpMappingsWithDataBinding = _booleanMappingProvider.GetNTPMappingsWithDataBinding();

            var ntpMappingsDict = ntpMappings
                .Where(m => !string.IsNullOrWhiteSpace(m.Title))
                .ToDictionary(m => m.Title, m => m);

            var ntpMappingsWithDataBindingDict = ntpMappingsWithDataBinding
                .ToDictionary(m => m.Binding, m => m);

            var existingGroup = await _dbContext
                .ProjectFormGroupTemplates
                .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Title == _GroupTitle && x.IsDeleted == false);



            if (existingGroup == null)
            {
                existingGroup = new ProjectFormGroupTemplate
                {
                    TenantId = tenantId,
                    Title = _GroupTitle,
                    IsActive = true,
                };
                await _dbContext.ProjectFormGroupTemplates.AddAsync(existingGroup);
                await _dbContext.SaveChangesAsync();
                Console.WriteLine($"Created group: {existingGroup.Title}");
            }

            var existingMapping = await _dbContext
                .DoorStepCustomFormIncludeWithMappings
                .Where(x => x.IncludeWithId == CustomFormIncludesWithTypes.NTP && x.IsDeleted == false && x.TenantId == tenantId).OrderByDescending(x => x.CreationTime)
                .FirstOrDefaultAsync();



            DoorStepCustomForm doorStepPostCadCustomForm = new DoorStepCustomForm();
            if (existingMapping == null)
            {
                doorStepPostCadCustomForm = new DoorStepCustomForm
                {
                    TenantId = tenantId,
                    Title = _DoorStepCustomFormNtpTitle,
                    IsActive = true,
                    CreationTime = DateTime.Now,
                    IsDraft = false,
                    RevisionNumber = 1
                };

                await _dbContext
                    .DoorStepCustomForms
                    .AddAsync(doorStepPostCadCustomForm);

                await _dbContext.SaveChangesAsync();

                var mapping = new DoorStepCustomFormIncludeWithMapping
                {
                    IncludeWithId = CustomFormIncludesWithTypes.NTP,
                    FormId = doorStepPostCadCustomForm.Id,
                    TenantId = tenantId,
                };

                await _dbContext.DoorStepCustomFormIncludeWithMappings.AddAsync(mapping);

                await _dbContext.SaveChangesAsync();
            }
            else
            {
                var existingForm = await _dbContext
                   .DoorStepCustomForms
                   .Where(x => x.Id == existingMapping.FormId && x.TenantId == tenantId && x.IsDeleted == false && x.IsActive == true)
                   .FirstOrDefaultAsync();

                if (existingForm != null)
                {
                    doorStepPostCadCustomForm.Id = existingMapping.FormId;

                }
                else
                {
                    doorStepPostCadCustomForm = new DoorStepCustomForm
                    {
                        TenantId = tenantId,
                        Title = _DoorStepCustomFormNtpTitle,
                        IsActive = true,
                        CreationTime = DateTime.Now,
                        IsDraft = false,
                        RevisionNumber = 1
                    };

                    await _dbContext.DoorStepCustomForms.AddAsync(doorStepPostCadCustomForm);
                    await _dbContext.SaveChangesAsync();
                }

                existingMapping.FormId = doorStepPostCadCustomForm.Id;
                await _dbContext.SaveChangesAsync();
            }


            ProjectFormTemplate existingProjectFormTemplate = await _dbContext
                .ProjectFormTemplates
                .Where(x => x.GroupId == existingGroup.Id && x.IsDeleted == false && x.TenantId == tenantId)
                .FirstOrDefaultAsync();

            if (existingProjectFormTemplate != null)
            {
                if (existingProjectFormTemplate.FormId != doorStepPostCadCustomForm.Id)
                {
                    existingProjectFormTemplate.FormId = doorStepPostCadCustomForm.Id;
                    await _dbContext.SaveChangesAsync();
                }

            }
            else
            {
                await _dbContext.ProjectFormTemplates.AddAsync(new ProjectFormTemplate
                {
                    TenantId = tenantId,
                    Title = _DoorStepCustomFormNtpTitle,
                    FormId = doorStepPostCadCustomForm.Id,
                    GroupId = existingGroup.Id,
                    CreationTime = DateTime.Now
                });

                await _dbContext.SaveChangesAsync();
            }


            int generalSectionId = 0;
            int secondaryCustomerSectionId = 0;
            int financingSectionId = 0;
            int equipmentSectionId = 0;
            int addersSectionId = 0;
            int utilityBillSectionId = 0;
            int hoaInformationSectionId = 0;
            int welcomeCallSectionId = 0;

            var existingSections = await _dbContext.DoorStepCustomFormSections
              .Where(x => x.TenantId == tenantId && x.FormId == doorStepPostCadCustomForm.Id && x.IsDeleted == false)
              .ToListAsync();
            
            // general Section
            var generalSection = existingSections
                .FirstOrDefault(x => x.Title == NTPSections.General);

            if (generalSection == null)
            {
                generalSection = new DoorStepCustomFormSection
                {
                    TenantId = tenantId,
                    FormId = doorStepPostCadCustomForm.Id,
                    Title = NTPSections.General,
                    CreationTime = DateTime.Now,
                    IsVerificationSection = true,
                    Order = 1,
                };
                await _dbContext.DoorStepCustomFormSections.AddAsync(generalSection);
                await _dbContext.SaveChangesAsync();

            }

            generalSectionId = generalSection?.Id ?? 0;

            // secondaryCustomer Section
            var secondaryCustomerSection = existingSections
               .FirstOrDefault(x => x.Title == NTPSections.SecondaryCustomer);

            if (secondaryCustomerSection == null)
            {
                secondaryCustomerSection = new DoorStepCustomFormSection
                {
                    TenantId = tenantId,
                    FormId = doorStepPostCadCustomForm.Id,
                    Title = NTPSections.SecondaryCustomer,
                    CreationTime = DateTime.Now,
                    IsVerificationSection = true,
                    Order = 2,
                };
                await _dbContext.DoorStepCustomFormSections.AddAsync(secondaryCustomerSection);
                await _dbContext.SaveChangesAsync();
            }
                
            secondaryCustomerSectionId = secondaryCustomerSection?.Id ?? 0;

            // financing Section
            var financingSection = existingSections
               .FirstOrDefault(x => x.Title == NTPSections.Financing);

            if (financingSection == null)
            {
                financingSection = new DoorStepCustomFormSection
                {
                    TenantId = tenantId,
                    FormId = doorStepPostCadCustomForm.Id,
                    Title = NTPSections.Financing,
                    CreationTime = DateTime.Now,
                    IsVerificationSection = true,
                    Order = 3,
                };
                await _dbContext.DoorStepCustomFormSections.AddAsync(financingSection);
                await _dbContext.SaveChangesAsync();
            }
                
            financingSectionId = financingSection?.Id ?? 0;

            //Equipment Section

            var equipmentSection = existingSections
               .FirstOrDefault(x => x.Title == NTPSections.Equipment);

            if (equipmentSection == null)
            {
                equipmentSection = new DoorStepCustomFormSection
                {
                    TenantId = tenantId,
                    FormId = doorStepPostCadCustomForm.Id,
                    Title = NTPSections.Equipment,
                    CreationTime = DateTime.Now,
                    IsVerificationSection = true,
                    Order = 4,
                };
                await _dbContext.DoorStepCustomFormSections.AddAsync(equipmentSection);
                await _dbContext.SaveChangesAsync();
            }

            equipmentSectionId = equipmentSection?.Id ?? 0;

            //"Adders";

            var addersSection = existingSections
            .FirstOrDefault(x => x.Title == NTPSections.Adders);

            if (addersSection == null)
            {
                addersSection = new DoorStepCustomFormSection
                {
                    TenantId = tenantId,
                    FormId = doorStepPostCadCustomForm.Id,
                    Title = NTPSections.Adders,
                    CreationTime = DateTime.Now,
                    IsVerificationSection = true,
                    Order = 5,
                };
                await _dbContext.DoorStepCustomFormSections.AddAsync(addersSection);
                await _dbContext.SaveChangesAsync();
            }

            addersSectionId = addersSection?.Id ?? 0;

            //"Utility Bill";

            var utilityBillSection = existingSections.FirstOrDefault(x => x.Title == NTPSections.UtilityBill);
            if (utilityBillSection == null)
            {
                utilityBillSection = new DoorStepCustomFormSection
                {
                    TenantId = tenantId,
                    FormId = doorStepPostCadCustomForm.Id,
                    Title = NTPSections.UtilityBill,
                    CreationTime = DateTime.Now,
                    IsVerificationSection = true,
                    Order = 6,
                };
                await _dbContext.DoorStepCustomFormSections.AddAsync(utilityBillSection);
                await _dbContext.SaveChangesAsync();
            }

            utilityBillSectionId = utilityBillSection?.Id ?? 0;

            //"HOA Information";
            var hoaInformationSection = existingSections.FirstOrDefault(x => x.Title == NTPSections.HOAInformation);

            if (hoaInformationSection == null)
            {
                hoaInformationSection = new DoorStepCustomFormSection
                {
                    TenantId = tenantId,
                    FormId = doorStepPostCadCustomForm.Id,
                    Title = NTPSections.HOAInformation,
                    CreationTime = DateTime.Now,
                    IsVerificationSection = true,
                    Order = 7,
                };
                await _dbContext.DoorStepCustomFormSections.AddAsync(hoaInformationSection);
                await _dbContext.SaveChangesAsync();
            }

            hoaInformationSectionId = hoaInformationSection?.Id ?? 0;
            //"Welcome Call";

            var welcomeCallSection = existingSections.FirstOrDefault(x => x.Title == NTPSections.WelcomeCall);
            if (welcomeCallSection == null)
            {
                welcomeCallSection = new DoorStepCustomFormSection
                {
                    TenantId = tenantId,
                    FormId = doorStepPostCadCustomForm.Id,
                    Title = NTPSections.WelcomeCall,
                    CreationTime = DateTime.Now,
                    IsVerificationSection = true,
                    Order = 8,
                };
                await _dbContext.DoorStepCustomFormSections.AddAsync(welcomeCallSection);
                await _dbContext.SaveChangesAsync();
            }

            welcomeCallSectionId = welcomeCallSection?.Id ?? 0;

            if (generalSection != null && generalSectionId != 0)
            {
                var existingGeneralFields = await _dbContext.DoorStepCustomFormSectionFields
                    .Where(x => x.TenantId == tenantId && x.SectionId == generalSectionId && x.IsDeleted == false)
                    .ToListAsync();

                var existingParentFieldIds = existingGeneralFields.Select(f => f.Id).ToList();

                var existingFieldMap = existingGeneralFields.ToDictionary(f => f.Title, f => f.Id);
                var parentFieldIdMap = existingFieldMap.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

                var validParentOptionIds = await _dbContext.DoorStepCustomFormCustomControlTypeValues
                 .Where(o => existingParentFieldIds.Contains(o.SectionFieldId))
                 .Select(o => o.Id)
                 .ToListAsync();

                var existingConditionalFields = await _dbContext.DoorStepCustomFormSectionConditionalFields
                    .Include(x => x.ControlType)
                    .Where(x => x.TenantId == tenantId && validParentOptionIds.Contains(x.ParentId) && x.IsDeleted == false)
                    .ToListAsync();

                var existingConditionalFieldMap = existingConditionalFields.ToDictionary(f => GetConditionalKey(f.Title, f.ControlType.SectionFieldId.ToString()), f => f.Id);

                var generalParentFieldsToCreate = new List<DoorStepCustomFormSectionField>();
                var generalOptionsToLink = new List<(DoorStepCustomFormCustomControlTypeValue Option, string title)>();

                int orderCounter = existingGeneralFields.Any() ? existingGeneralFields.Max(f => f.Order) + 1 : 1;

                var generalParentDefinitions = new (string title, string type, int fieldType, bool isRequired, int controlTypeId, CustomFormDataBindingEnum enumValue)[]
                {
                    ("First Name", DoorStepCustomControlTypeStrings.TextSingleLine, 3,  true, controlTypeIds.TextSingleLineId, CustomFormDataBindingEnum.PrimaryCustomerFirstName),
                    ("Last Name", DoorStepCustomControlTypeStrings.TextSingleLine, 3,  true, controlTypeIds.TextSingleLineId, CustomFormDataBindingEnum.PrimaryCustomerLastName),
                    ("SSN", DoorStepCustomControlTypeStrings.SSNInput, 3,  false, controlTypeIds.SSNControlId, CustomFormDataBindingEnum.SSN),
                    ("Date of Birth", DoorStepCustomControlTypeStrings.Date, 3,  false, controlTypeIds.DateControlId, CustomFormDataBindingEnum.DateOfBirth),
                    ("Phone", DoorStepCustomControlTypeStrings.Phone, 3,  true, controlTypeIds.PhoneId, CustomFormDataBindingEnum.PrimaryCustomerPhoneNumber),
                    ("Email", DoorStepCustomControlTypeStrings.Email, 3,  true, controlTypeIds.EmailId, CustomFormDataBindingEnum.PrimaryCustomerEmail),
                    ("Customer Address", DoorStepCustomControlTypeStrings.AddressControl, 3,  true, controlTypeIds.AddressId, CustomFormDataBindingEnum.CustomerAddress),
                };

                foreach (var def in generalParentDefinitions)
                {
                    if (existingFieldMap.ContainsKey(def.title))
                    {
                        parentFieldIdMap[def.title] = existingFieldMap[def.title];
                        continue;
                    }

                    var newField = new DoorStepCustomFormSectionField
                    {
                        TenantId = tenantId,
                        SectionId = generalSectionId,
                        Title = def.title,
                        CreationTime = DateTime.Now,
                        Order = orderCounter++,
                        FieldType = def.fieldType,
                        IsRequired = def.isRequired,
                        Type = def.type,
                        CustomControlTypeId = def.controlTypeId,
                        DataBinding = def.enumValue
                    };
                    generalParentFieldsToCreate.Add(newField);

                    if ((def.type == DoorStepCustomControlTypeStrings.SelectButton || def.type == "Dropdown") && ntpMappingsDict.TryGetValue(def.title, out var optionsForField))
                    {
                        int optionOrderCounter = 1;

                        if (optionsForField != null)
                        {

                            if (!string.IsNullOrEmpty(optionsForField.FalseOptionName) && !string.IsNullOrWhiteSpace(optionsForField.FalseOptionName))
                            {
                                generalOptionsToLink.Add((new DoorStepCustomFormCustomControlTypeValue
                                {
                                    Value = optionsForField.FalseOptionName,
                                    Order = optionOrderCounter++,
                                }, def.title));
                            }

                            if (!string.IsNullOrWhiteSpace(optionsForField.TrueOptionName) && !string.IsNullOrEmpty(optionsForField.TrueOptionName))
                            {
                                generalOptionsToLink.Add((new DoorStepCustomFormCustomControlTypeValue
                                {
                                    Value = optionsForField.TrueOptionName,
                                    Order = optionOrderCounter++,
                                }, def.title));
                            }

                        }
                    }
                }

                if (generalParentFieldsToCreate.Any())
                {
                    await _dbContext.DoorStepCustomFormSectionFields.AddRangeAsync(generalParentFieldsToCreate);
                    await _dbContext.SaveChangesAsync();
                    foreach (var field in generalParentFieldsToCreate) { parentFieldIdMap[field.Title] = field.Id; }
                }

                var optionsToSave = new List<DoorStepCustomFormCustomControlTypeValue>();
                foreach (var (option, binding) in generalOptionsToLink)
                {
                    if (parentFieldIdMap.TryGetValue(binding, out var fieldId))
                    {
                        option.SectionFieldId = fieldId;
                        optionsToSave.Add(option);
                    }
                }

                if (optionsToSave.Any())
                {
                    await _dbContext.DoorStepCustomFormCustomControlTypeValues.AddRangeAsync(optionsToSave);
                    await _dbContext.SaveChangesAsync();
                }

            }

            if (secondaryCustomerSection != null && secondaryCustomerSectionId != 0)
            {
                var existingSecondaryCustomerSectionFields = await _dbContext.DoorStepCustomFormSectionFields
                .Where(x => x.TenantId == tenantId && x.SectionId == secondaryCustomerSectionId && x.IsDeleted == false)
                .ToListAsync();

                var existingParentFieldIds = existingSecondaryCustomerSectionFields.Select(f => f.Id).ToList();
                var existingFieldMap = existingSecondaryCustomerSectionFields.ToDictionary(f => f.Title, f => f.Id);
                var parentFieldIdMap = existingFieldMap.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

                var validParentOptionIds = await _dbContext.DoorStepCustomFormCustomControlTypeValues
                 .Where(o => existingParentFieldIds.Contains(o.SectionFieldId))
                 .Select(o => o.Id)
                 .ToListAsync();

                var existingConditionalFields = await _dbContext.DoorStepCustomFormSectionConditionalFields
                    .Where(x => x.TenantId == tenantId && validParentOptionIds.Contains(x.ParentId))
                    .ToListAsync();


                var existingConditionalFieldMap = existingConditionalFields.ToDictionary(f => f.Title, f => f.Id);

                var secondaryCustomerParentFieldsToCreate = new List<DoorStepCustomFormSectionField>();
                var secondaryCustomerOptionsToLink = new List<(DoorStepCustomFormCustomControlTypeValue Option, string title)>();

                int orderCounter = existingSecondaryCustomerSectionFields.Any() ? existingSecondaryCustomerSectionFields.Max(f => f.Order) + 1 : 1;

                var secondaryCustomerParentDefinitions = new (string title, string type, int fieldType, bool isRequired, int controlTypeId, CustomFormDataBindingEnum? bindingEnum)[]
                {
                    ("First Name", DoorStepCustomControlTypeStrings.TextSingleLine, 3,  false, controlTypeIds.TextSingleLineId, CustomFormDataBindingEnum.SecondaryCustomerFirstName),
                    ("Last Name", DoorStepCustomControlTypeStrings.TextSingleLine, 3,  false, controlTypeIds.TextSingleLineId, CustomFormDataBindingEnum.SecondaryCustomerLastName),
                    ("Phone", DoorStepCustomControlTypeStrings.Phone, 3,  false, controlTypeIds.PhoneId, CustomFormDataBindingEnum.SecondaryCustomerPhoneNumber),
                    ("Email", DoorStepCustomControlTypeStrings.Email, 3,  false, controlTypeIds.EmailId, CustomFormDataBindingEnum.SecondaryCustomerEmail),

                };

                foreach (var def in secondaryCustomerParentDefinitions)
                {
                    if (existingFieldMap.ContainsKey(def.title))
                    {
                        parentFieldIdMap[def.title] = existingFieldMap[def.title];
                        continue;
                    }

                    var newField = new DoorStepCustomFormSectionField
                    {
                        TenantId = tenantId,
                        SectionId = secondaryCustomerSectionId,
                        Title = def.title,
                        CreationTime = DateTime.Now,
                        Order = orderCounter++,
                        FieldType = def.fieldType,
                        IsRequired = def.isRequired,
                        Type = def.type,
                        CustomControlTypeId = def.controlTypeId,
                        DataBinding = def.bindingEnum.HasValue ? def.bindingEnum.Value : null
                    };
                    secondaryCustomerParentFieldsToCreate.Add(newField);

                    if ((def.type == MigrationConsts.DoorStepCustomControlTypeStrings.SelectButton || def.type == "Dropdown") && ntpMappingsDict.TryGetValue(def.title, out var optionsForField))
                    {
                        int optionOrderCounter = 1;

                        if (optionsForField != null)
                        {

                            if (!string.IsNullOrEmpty(optionsForField.FalseOptionName) && !string.IsNullOrWhiteSpace(optionsForField.FalseOptionName))
                            {
                                secondaryCustomerOptionsToLink.Add((new DoorStepCustomFormCustomControlTypeValue
                                {
                                    Value = optionsForField.FalseOptionName,
                                    Order = optionOrderCounter++,
                                    GlobalOptionId = def.bindingEnum.HasValue ? GetCustomFormGlobalFieldOptionList(tenantId).Result
                                        .FirstOrDefault(o => o.Name == optionsForField.FalseOptionName)?.Id : null
                                }, def.title));
                            }

                            if (!string.IsNullOrWhiteSpace(optionsForField.TrueOptionName) && !string.IsNullOrEmpty(optionsForField.TrueOptionName))
                            {
                                secondaryCustomerOptionsToLink.Add((new DoorStepCustomFormCustomControlTypeValue
                                {
                                    Value = optionsForField.TrueOptionName,
                                    Order = optionOrderCounter++,
                                    GlobalOptionId = def.bindingEnum.HasValue ? GetCustomFormGlobalFieldOptionList(tenantId).Result
                                        .FirstOrDefault(o => o.Name == optionsForField.TrueOptionName)?.Id : null
                                }, def.title));
                            }

                        }
                    }
                }

                if (secondaryCustomerParentFieldsToCreate.Any())
                {
                    await _dbContext.DoorStepCustomFormSectionFields.AddRangeAsync(secondaryCustomerParentFieldsToCreate);
                    await _dbContext.SaveChangesAsync();
                    foreach (var field in secondaryCustomerParentFieldsToCreate) { parentFieldIdMap[field.Title] = field.Id; }
                }

                var optionsToSave = new List<DoorStepCustomFormCustomControlTypeValue>();
                foreach (var (option, binding) in secondaryCustomerOptionsToLink)
                {
                    if (parentFieldIdMap.TryGetValue(binding, out var fieldId))
                    {
                        option.SectionFieldId = fieldId;
                        optionsToSave.Add(option);
                    }
                }

                if (optionsToSave.Any())
                {
                    await _dbContext.DoorStepCustomFormCustomControlTypeValues.AddRangeAsync(optionsToSave);
                    await _dbContext.SaveChangesAsync();
                }

            }

            if(financingSection != null && financingSectionId != 0)
            {
                // Similar logic for financing section fields and options can be implemented here, following the pattern established for the general and secondary customer sections.
                var existingFinancingSectionFields = await _dbContext.DoorStepCustomFormSectionFields
                 .Where(x => x.TenantId == tenantId && x.SectionId == financingSectionId && x.IsDeleted == false)
                 .ToListAsync();

                var existingParentFieldIds = existingFinancingSectionFields.Select(f => f.Id).ToList();
                var existingFieldMap = existingFinancingSectionFields.ToDictionary(f => f.Title, f => f.Id);
                var parentFieldIdMap = existingFieldMap.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

                var validParentOptionIds = await _dbContext.DoorStepCustomFormCustomControlTypeValues
                 .Where(o => existingParentFieldIds.Contains(o.SectionFieldId))
                 .Select(o => o.Id)
                 .ToListAsync();

                var existingConditionalFields = await _dbContext.DoorStepCustomFormSectionConditionalFields
                    .Where(x => x.TenantId == tenantId && validParentOptionIds.Contains(x.ParentId))
                    .ToListAsync();


                var existingConditionalFieldMap = existingConditionalFields.ToDictionary(f => f.Title, f => f.Id);

                var financingParentFieldsToCreate = new List<DoorStepCustomFormSectionField>();
                var financingOptionsToLink = new List<(DoorStepCustomFormCustomControlTypeValue Option, string title)>();

                int orderCounter = existingFinancingSectionFields.Any() ? existingFinancingSectionFields.Max(f => f.Order) + 1 : 1;

                var financingParentDefinitions = new (string title, string type, int fieldType, bool isRequired, int controlTypeId, CustomFormDataBindingEnum bindingEnum)[]
                {
                    ("Has Financing been Approved?", DoorStepCustomControlTypeStrings.SelectButton, 3,  true, controlTypeIds.SelectButtonId, CustomFormDataBindingEnum.HasFinancingBeenApproved),
                    ("Financing Amount", DoorStepCustomControlTypeStrings.Currency, 3,  true, controlTypeIds.CurrencyId, CustomFormDataBindingEnum.FinancingAmount),
                    ("Gross Cost", DoorStepCustomControlTypeStrings.Currency, 3,  false, controlTypeIds.CurrencyId, CustomFormDataBindingEnum.GrossCost),
                    ("Dealers Fee", DoorStepCustomControlTypeStrings.Currency, 3,  false, controlTypeIds.CurrencyId, CustomFormDataBindingEnum.DealerFee),

                };

                foreach (var def in financingParentDefinitions)
                {
                    if (existingFieldMap.ContainsKey(def.title))
                    {
                        parentFieldIdMap[def.title] = existingFieldMap[def.title];
                        continue;
                    }

                    var newField = new DoorStepCustomFormSectionField
                    {
                        TenantId = tenantId,
                        SectionId = financingSectionId,
                        Title = def.title,
                        CreationTime = DateTime.Now,
                        Order = orderCounter++,
                        FieldType = def.fieldType,
                        IsRequired = def.isRequired,
                        Type = def.type,
                        CustomControlTypeId = def.controlTypeId,
                        DataBinding =  def.bindingEnum
                    };
                    financingParentFieldsToCreate.Add(newField);

                    if ((def.type == MigrationConsts.DoorStepCustomControlTypeStrings.SelectButton || def.type == "Dropdown")  && ntpMappingsWithDataBindingDict.TryGetValue(def.bindingEnum, out var optionsForField))
                    {
                        int optionOrderCounter = 1;

                        if (optionsForField != null)
                        {

                            if (!string.IsNullOrEmpty(optionsForField.FalseOptionName) && !string.IsNullOrWhiteSpace(optionsForField.FalseOptionName))
                            {
                                financingOptionsToLink.Add((new DoorStepCustomFormCustomControlTypeValue
                                {
                                    Value = optionsForField.FalseOptionName,
                                    Order = optionOrderCounter++,
                                    GlobalOptionId = GetCustomFormGlobalFieldOptionList(tenantId).Result
                                        .FirstOrDefault(o => o.Name == optionsForField.FalseOptionName)?.Id 
                                }, def.title));
                            }

                            if (!string.IsNullOrWhiteSpace(optionsForField.TrueOptionName) && !string.IsNullOrEmpty(optionsForField.TrueOptionName))
                            {
                                financingOptionsToLink.Add((new DoorStepCustomFormCustomControlTypeValue
                                {
                                    Value = optionsForField.TrueOptionName,
                                    Order = optionOrderCounter++,
                                    GlobalOptionId = GetCustomFormGlobalFieldOptionList(tenantId).Result
                                        .FirstOrDefault(o => o.Name == optionsForField.TrueOptionName)?.Id ?? 0
                                }, def.title));
                            }

                        }
                    }
                }

                if (financingParentFieldsToCreate.Any())
                {
                    await _dbContext.DoorStepCustomFormSectionFields.AddRangeAsync(financingParentFieldsToCreate);
                    await _dbContext.SaveChangesAsync();
                    foreach (var field in financingParentFieldsToCreate) { parentFieldIdMap[field.Title] = field.Id; }
                }

                var optionsToSave = new List<DoorStepCustomFormCustomControlTypeValue>();
                foreach (var (option, binding) in financingOptionsToLink)
                {
                    if (parentFieldIdMap.TryGetValue(binding, out var fieldId))
                    {
                        option.SectionFieldId = fieldId;
                        optionsToSave.Add(option);
                    }
                }

                if (optionsToSave.Any())
                {
                    await _dbContext.DoorStepCustomFormCustomControlTypeValues.AddRangeAsync(optionsToSave);
                    await _dbContext.SaveChangesAsync();
                }

            }

            if(equipmentSection != null && equipmentSectionId != 0)
            {
                // Similar logic for equipment section fields and options can be implemented here, following the pattern established for the general and secondary customer sections.
                var existingEquipmentSectionFields = await _dbContext.DoorStepCustomFormSectionFields
                 .Where(x => x.TenantId == tenantId && x.SectionId == equipmentSectionId && x.IsDeleted == false)
                 .ToListAsync();

                var existingParentFieldIds = existingEquipmentSectionFields.Select(f => f.Id).ToList();
                var existingFieldMap = existingEquipmentSectionFields.ToDictionary(f => f.Title, f => f.Id);
                var parentFieldIdMap = existingFieldMap.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

                var validParentOptionIds = await _dbContext.DoorStepCustomFormCustomControlTypeValues
                 .Where(o => existingParentFieldIds.Contains(o.SectionFieldId))
                 .Select(o => o.Id)
                 .ToListAsync();

                var existingConditionalFields = await _dbContext.DoorStepCustomFormSectionConditionalFields
                    .Where(x => x.TenantId == tenantId && validParentOptionIds.Contains(x.ParentId))
                    .ToListAsync();


                var existingConditionalFieldMap = existingConditionalFields.ToDictionary(f => f.Title, f => f.Id);

                var equipmentParentFieldsToCreate = new List<DoorStepCustomFormSectionField>();
                var equipmentOptionsToLink = new List<(DoorStepCustomFormCustomControlTypeValue Option, string title)>();

                int orderCounter = existingEquipmentSectionFields.Any() ? existingEquipmentSectionFields.Max(f => f.Order) + 1 : 1;

                var equipmentParentDefinitions = new (string title, string type, int fieldType, bool isRequired, int controlTypeId, CustomFormDataBindingEnum bindingEnum)[]
                {
                    ("Panels", DoorStepCustomControlTypeStrings.TextSingleLine, 3,  true, controlTypeIds.TextSingleLineId, CustomFormDataBindingEnum.Panels),
                    ("Inverter", DoorStepCustomControlTypeStrings.TextSingleLine, 3,  false, controlTypeIds.TextSingleLineId, CustomFormDataBindingEnum.AccountInverterName),
                    ("System Size", DoorStepCustomControlTypeStrings.Number, 3,  false, controlTypeIds.NumberId, CustomFormDataBindingEnum.SystemSize),

                };

                foreach (var def in equipmentParentDefinitions)
                {
                    if (existingFieldMap.ContainsKey(def.title))
                    {
                        parentFieldIdMap[def.title] = existingFieldMap[def.title];
                        continue;
                    }

                    var newField = new DoorStepCustomFormSectionField
                    {
                        TenantId = tenantId,
                        SectionId = equipmentSectionId,
                        Title = def.title,
                        CreationTime = DateTime.Now,
                        Order = orderCounter++,
                        FieldType = def.fieldType,
                        IsRequired = def.isRequired,
                        Type = def.type,
                        CustomControlTypeId = def.controlTypeId,
                        DataBinding =  def.bindingEnum
                    };
                    equipmentParentFieldsToCreate.Add(newField);

                    if ((def.type == MigrationConsts.DoorStepCustomControlTypeStrings.SelectButton || def.type == "Dropdown")  && ntpMappingsWithDataBindingDict.TryGetValue(def.bindingEnum, out var optionsForField))
                    {
                        int optionOrderCounter = 1;

                        if (optionsForField != null)
                        {

                            if (!string.IsNullOrEmpty(optionsForField.FalseOptionName) && !string.IsNullOrWhiteSpace(optionsForField.FalseOptionName))
                            {
                                equipmentOptionsToLink.Add((new DoorStepCustomFormCustomControlTypeValue
                                {
                                    Value = optionsForField.FalseOptionName,
                                    Order = optionOrderCounter++,
                                    GlobalOptionId = GetCustomFormGlobalFieldOptionList(tenantId).Result
                                        .FirstOrDefault(o => o.Name == optionsForField.FalseOptionName)?.Id 
                                }, def.title));
                            }

                            if (!string.IsNullOrWhiteSpace(optionsForField.TrueOptionName) && !string.IsNullOrEmpty(optionsForField.TrueOptionName))
                            {
                                equipmentOptionsToLink.Add((new DoorStepCustomFormCustomControlTypeValue
                                {
                                    Value = optionsForField.TrueOptionName,
                                    Order = optionOrderCounter++,
                                    GlobalOptionId = GetCustomFormGlobalFieldOptionList(tenantId).Result
                                        .FirstOrDefault(o => o.Name == optionsForField.TrueOptionName)?.Id ?? 0
                                }, def.title));
                            }

                        }
                    }
                }

                if (equipmentParentFieldsToCreate.Any())
                {
                    await _dbContext.DoorStepCustomFormSectionFields.AddRangeAsync(equipmentParentFieldsToCreate);
                    await _dbContext.SaveChangesAsync();
                    foreach (var field in equipmentParentFieldsToCreate) { parentFieldIdMap[field.Title] = field.Id; }
                }

                var optionsToSave = new List<DoorStepCustomFormCustomControlTypeValue>();
                foreach (var (option, binding) in equipmentOptionsToLink)
                {
                    if (parentFieldIdMap.TryGetValue(binding, out var fieldId))
                    {
                        option.SectionFieldId = fieldId;
                        optionsToSave.Add(option);
                    }
                }

                if (optionsToSave.Any())
                {
                    await _dbContext.DoorStepCustomFormCustomControlTypeValues.AddRangeAsync(optionsToSave);
                    await _dbContext.SaveChangesAsync();
                }

            }

            if (addersSection != null && addersSectionId != 0)
            {
                // Similar logic for adders section fields and options can be implemented here, following the pattern established for the general and secondary customer sections.
                var existingAddersSectionFields = await _dbContext.DoorStepCustomFormSectionFields
                 .Where(x => x.TenantId == tenantId && x.SectionId == addersSectionId && x.IsDeleted == false)
                 .ToListAsync();

                var existingParentFieldIds = existingAddersSectionFields.Select(f => f.Id).ToList();
                var existingFieldMap = existingAddersSectionFields.ToDictionary(f => f.Title, f => f.Id);
                var parentFieldIdMap = existingFieldMap.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

                var validParentOptionIds = await _dbContext.DoorStepCustomFormCustomControlTypeValues
                 .Where(o => existingParentFieldIds.Contains(o.SectionFieldId))
                 .Select(o => o.Id)
                 .ToListAsync();

                var existingConditionalFields = await _dbContext.DoorStepCustomFormSectionConditionalFields
                    .Where(x => x.TenantId == tenantId && validParentOptionIds.Contains(x.ParentId))
                    .ToListAsync();


                var existingConditionalFieldMap = existingConditionalFields.ToDictionary(f => f.Title, f => f.Id);

                var addersParentFieldsToCreate = new List<DoorStepCustomFormSectionField>();
                var addersOptionsToLink = new List<(DoorStepCustomFormCustomControlTypeValue Option, string title)>();

                int orderCounter = existingAddersSectionFields.Any() ? existingAddersSectionFields.Max(f => f.Order) + 1 : 1;

                var addersParentDefinitions = new (string title, string type, int fieldType, bool isRequired, int controlTypeId, CustomFormDataBindingEnum bindingEnum)[]
                {
                    ("Adders", DoorStepCustomControlTypeStrings.AdderControl, 3,  true, controlTypeIds.AdderControlId, CustomFormDataBindingEnum.AddersDetails),
                   
                };

                foreach (var def in addersParentDefinitions)
                {
                    if (existingFieldMap.ContainsKey(def.title))
                    {
                        parentFieldIdMap[def.title] = existingFieldMap[def.title];
                        continue;
                    }

                    var newField = new DoorStepCustomFormSectionField
                    {
                        TenantId = tenantId,
                        SectionId = addersSectionId,
                        Title = def.title,
                        CreationTime = DateTime.Now,
                        Order = orderCounter++,
                        FieldType = def.fieldType,
                        IsRequired = def.isRequired,
                        Type = def.type,
                        CustomControlTypeId = def.controlTypeId,
                        DataBinding = def.bindingEnum
                    };
                    addersParentFieldsToCreate.Add(newField);

                }

                if (addersParentFieldsToCreate.Any())
                {
                    await _dbContext.DoorStepCustomFormSectionFields.AddRangeAsync(addersParentFieldsToCreate);
                    await _dbContext.SaveChangesAsync();
                    foreach (var field in addersParentFieldsToCreate) { parentFieldIdMap[field.Title] = field.Id; }
                }

                var optionsToSave = new List<DoorStepCustomFormCustomControlTypeValue>();
                foreach (var (option, binding) in addersOptionsToLink)
                {
                    if (parentFieldIdMap.TryGetValue(binding, out var fieldId))
                    {
                        option.SectionFieldId = fieldId;
                        optionsToSave.Add(option);
                    }
                }

                if (optionsToSave.Any())
                {
                    await _dbContext.DoorStepCustomFormCustomControlTypeValues.AddRangeAsync(optionsToSave);
                    await _dbContext.SaveChangesAsync();
                }

            }

            if (utilityBillSection != null && utilityBillSectionId != 0)
            {
                var existingUtilityBillFields = await _dbContext.DoorStepCustomFormSectionFields
                    .Where(x => x.TenantId == tenantId && x.SectionId == utilityBillSectionId && x.IsDeleted == false)
                    .ToListAsync();

                var existingParentFieldIds = existingUtilityBillFields.Select(f => f.Id).ToList();
                var existingFieldMap = existingUtilityBillFields.ToDictionary(f => f.Title, f => f.Id);
                var parentFieldIdMap = existingFieldMap.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

                var validParentOptionIds = await _dbContext.DoorStepCustomFormCustomControlTypeValues
                 .Where(o => existingParentFieldIds.Contains(o.SectionFieldId))
                 .Select(o => o.Id)
                 .ToListAsync();

                var existingConditionalFields = await _dbContext.DoorStepCustomFormSectionConditionalFields
                    .Include(x => x.ControlType)
                    .Where(x => x.TenantId == tenantId && validParentOptionIds.Contains(x.ParentId) && x.IsDeleted == false)
                    .ToListAsync();

                var existingConditionalFieldMap = existingConditionalFields.ToDictionary(f => GetConditionalKey(f.Title, f.ControlType.SectionFieldId.ToString()), f => f.Id);

                var utilityBillParentFieldsToCreate = new List<DoorStepCustomFormSectionField>();
                var utilityBillOptionsToLink = new List<(DoorStepCustomFormCustomControlTypeValue Option, string title)>();

                int orderCounter = existingUtilityBillFields.Any() ? existingUtilityBillFields.Max(f => f.Order) + 1 : 1;

                var utilityBillParentDefinitions = new (string title, string type, int fieldType, bool isRequired, int controlTypeId)[]
                {
                    ("Has utility bill been received?", DoorStepCustomControlTypeStrings.SelectButton, 3,  true, controlTypeIds.SelectButtonId),
                };

                foreach (var def in utilityBillParentDefinitions)
                {
                    if (existingFieldMap.ContainsKey(def.title))
                    {
                        parentFieldIdMap[def.title] = existingFieldMap[def.title];
                        continue;
                    }

                    var newField = new DoorStepCustomFormSectionField
                    {
                        TenantId = tenantId,
                        SectionId = utilityBillSectionId,
                        Title = def.title,
                        CreationTime = DateTime.Now,
                        Order = orderCounter++,
                        FieldType = def.fieldType,
                        IsRequired = def.isRequired,
                        Type = def.type,
                        CustomControlTypeId = def.controlTypeId
                    };
                    utilityBillParentFieldsToCreate.Add(newField);

                    if ((def.type == DoorStepCustomControlTypeStrings.SelectButton || def.type == "Dropdown") && ntpMappingsDict.TryGetValue(def.title, out var optionsForField))
                    {
                        int optionOrderCounter = 1;

                        if (optionsForField != null)
                        {

                            if (!string.IsNullOrEmpty(optionsForField.FalseOptionName) && !string.IsNullOrWhiteSpace(optionsForField.FalseOptionName))
                            {
                                utilityBillOptionsToLink.Add((new DoorStepCustomFormCustomControlTypeValue
                                {
                                    Value = optionsForField.FalseOptionName,
                                    Order = optionOrderCounter++,
                                }, def.title));
                            }

                            if (!string.IsNullOrWhiteSpace(optionsForField.TrueOptionName) && !string.IsNullOrEmpty(optionsForField.TrueOptionName))
                            {
                                utilityBillOptionsToLink.Add((new DoorStepCustomFormCustomControlTypeValue
                                {
                                    Value = optionsForField.TrueOptionName,
                                    Order = optionOrderCounter++,
                                }, def.title));
                            }

                        }
                    }
                }

                if (utilityBillParentFieldsToCreate.Any())
                {
                    await _dbContext.DoorStepCustomFormSectionFields.AddRangeAsync(utilityBillParentFieldsToCreate);
                    await _dbContext.SaveChangesAsync();
                    foreach (var field in utilityBillParentFieldsToCreate) { parentFieldIdMap[field.Title] = field.Id; }
                }

                var optionsToSave = new List<DoorStepCustomFormCustomControlTypeValue>();
                foreach (var (option, binding) in utilityBillOptionsToLink)
                {
                    if (parentFieldIdMap.TryGetValue(binding, out var fieldId))
                    {
                        option.SectionFieldId = fieldId;
                        optionsToSave.Add(option);
                    }
                }

                if (optionsToSave.Any())
                {
                    await _dbContext.DoorStepCustomFormCustomControlTypeValues.AddRangeAsync(optionsToSave);
                    await _dbContext.SaveChangesAsync();
                }

                var utilityBillConditionalFieldsToCreate = new List<DoorStepCustomFormSectionConditionalField>();
                var utilityBillConditionalOptionsToLink = new List<(DoorStepCustomFormSectionConditionalOption Option, string Title)>();


                var utilityBillConditionalDefinitions = new (
                    string title,
                    string type,
                    bool isRequired,
                    string triggerBinding,
                    string triggerOptionValue,
                    int controlTypeId,
                    bool hasGlobalOptions)[]
                {
                    ("Does the Customer information match?", DoorStepCustomControlTypeStrings.SelectButton, true, "Has utility bill been received?", ntpMappings.Where(x => x.Title == "Has utility bill been received?").Select(x => x.TrueOptionName).FirstOrDefault(), controlTypeIds.SelectButtonId, false),
                };


                foreach (var def in utilityBillConditionalDefinitions)
                {


                    if (!parentFieldIdMap.TryGetValue(def.triggerBinding, out int parentFieldId) || parentFieldId <= 0)
                    {
                        Console.WriteLine($"WARNING: Parent Field ID for {def.triggerBinding} is invalid or missing. Skipping conditional field {def.title}.");
                        continue;
                    }

                    var conditionalKey = GetConditionalKey(def.title, parentFieldId.ToString());
                    if (existingConditionalFieldMap.ContainsKey(conditionalKey)) continue;

                    var triggerOptionEntity = await _dbContext.DoorStepCustomFormCustomControlTypeValues
                        .FirstOrDefaultAsync(o =>
                            o.SectionFieldId == parentFieldId &&
                            o.Value == def.triggerOptionValue);

                    if (triggerOptionEntity == null)
                    {
                        throw new InvalidOperationException($"FATAL: Trigger Option '{def.triggerOptionValue}' not found for Parent {def.triggerBinding}. Check if the option was saved correctly.");
                    }

                    var newConditionalField = new DoorStepCustomFormSectionConditionalField
                    {
                        TenantId = tenantId,
                        Title = def.title,
                        CreationTime = DateTime.Now,
                        Order = orderCounter++,
                        FieldType = 3,
                        IsRequired = def.isRequired,
                        Type = def.type,
                        ParentId = triggerOptionEntity.Id,
                        CustomControlTypeId = def.controlTypeId
                    };
                    utilityBillConditionalFieldsToCreate.Add(newConditionalField);

                    if (ntpMappingsDict.TryGetValue(def.title, out var optionsForBinding))
                    {
                        int optionOrderCounter = 1;

                        if (!string.IsNullOrEmpty(optionsForBinding.FalseOptionName) || !string.IsNullOrWhiteSpace(optionsForBinding.FalseOptionName))
                        {

                            utilityBillConditionalOptionsToLink.Add((new DoorStepCustomFormSectionConditionalOption
                            {
                                Value = optionsForBinding.FalseOptionName,
                                Order = optionOrderCounter++,
                                TenantId = tenantId,
                            }, def.title));
                        }

                        if (!string.IsNullOrEmpty(optionsForBinding.TrueOptionName) || !string.IsNullOrWhiteSpace(optionsForBinding.TrueOptionName))
                        {
                            string controlValue = optionsForBinding.TrueOptionName;

                            utilityBillConditionalOptionsToLink.Add((new DoorStepCustomFormSectionConditionalOption
                            {
                                Value = optionsForBinding.TrueOptionName,
                                Order = optionOrderCounter++,
                                TenantId = tenantId,
                            }, def.title));
                        }
                    }
                }

                if (utilityBillConditionalFieldsToCreate.Any())
                {
                    await _dbContext.DoorStepCustomFormSectionConditionalFields.AddRangeAsync(utilityBillConditionalFieldsToCreate);
                    await _dbContext.SaveChangesAsync();

                    var conditionalOptionsToSave = utilityBillConditionalOptionsToLink
                        .Select(t => { t.Option.ParentId = utilityBillConditionalFieldsToCreate.Where(x => x.Title == "Does the Customer information match?").Select(x => x.Id).FirstOrDefault(); return t.Option; })
                        .ToList();

                    if (conditionalOptionsToSave.Any())
                    {
                        await _dbContext.DoorStepCustomFormSectionConditionalOptions.AddRangeAsync(conditionalOptionsToSave);
                        await _dbContext.SaveChangesAsync();
                    }
                }
            }

            if (hoaInformationSection != null && hoaInformationSectionId != 0)
            {
                var existingHOAFields = await _dbContext.DoorStepCustomFormSectionFields
                    .Where(x => x.TenantId == tenantId && x.SectionId == hoaInformationSectionId && x.IsDeleted == false)
                    .ToListAsync();

                var existingParentFieldIds = existingHOAFields.Select(f => f.Id).ToList();
                var existingFieldMap = existingHOAFields.ToDictionary(f => f.Title, f => f.Id);
                var parentFieldIdMap = existingFieldMap.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

                var validParentOptionIds = await _dbContext.DoorStepCustomFormCustomControlTypeValues
                 .Where(o => existingParentFieldIds.Contains(o.SectionFieldId))
                 .Select(o => o.Id)
                 .ToListAsync();

                var existingConditionalFields = await _dbContext.DoorStepCustomFormSectionConditionalFields
                    .Include(x => x.ControlType)
                    .Where(x => x.TenantId == tenantId && validParentOptionIds.Contains(x.ParentId) && x.IsDeleted == false)
                    .ToListAsync();

                var existingConditionalFieldMap = existingConditionalFields.ToDictionary(f => GetConditionalKey(f.Title, f.ControlType.SectionFieldId.ToString()), f => f.Id);

                var hoaInformationParentFieldsToCreate = new List<DoorStepCustomFormSectionField>();
                var hoaInformationOptionsToLink = new List<(DoorStepCustomFormCustomControlTypeValue Option, string title)>();

                int orderCounter = existingHOAFields.Any() ? existingHOAFields.Max(f => f.Order) + 1 : 1;

                var hoaInformationParentDefinitions = new (string title, string type, int fieldType, bool isRequired, int controlTypeId)[]
                {
                    ("Does the customer have an HOA?", DoorStepCustomControlTypeStrings.SelectButton, 3,  true, controlTypeIds.SelectButtonId),
                };

                foreach (var def in hoaInformationParentDefinitions)
                {
                    if (existingFieldMap.ContainsKey(def.title))
                    {
                        parentFieldIdMap[def.title] = existingFieldMap[def.title];
                        continue;
                    }

                    var newField = new DoorStepCustomFormSectionField
                    {
                        TenantId = tenantId,
                        SectionId = hoaInformationSectionId,
                        Title = def.title,
                        CreationTime = DateTime.Now,
                        Order = orderCounter++,
                        FieldType = def.fieldType,
                        IsRequired = def.isRequired,
                        Type = def.type,
                        CustomControlTypeId = def.controlTypeId,
                    };
                    hoaInformationParentFieldsToCreate.Add(newField);

                    if ((def.type == DoorStepCustomControlTypeStrings.SelectButton || def.type == "Dropdown") && ntpMappingsDict.TryGetValue(def.title, out var optionsForField))
                    {
                        int optionOrderCounter = 1;

                        if (optionsForField != null)
                        {

                            if (!string.IsNullOrEmpty(optionsForField.FalseOptionName) && !string.IsNullOrWhiteSpace(optionsForField.FalseOptionName))
                            {
                                hoaInformationOptionsToLink.Add((new DoorStepCustomFormCustomControlTypeValue
                                {
                                    Value = optionsForField.FalseOptionName,
                                    Order = optionOrderCounter++,
                                }, def.title));
                            }

                            if (!string.IsNullOrWhiteSpace(optionsForField.TrueOptionName) && !string.IsNullOrEmpty(optionsForField.TrueOptionName))
                            {
                                hoaInformationOptionsToLink.Add((new DoorStepCustomFormCustomControlTypeValue
                                {
                                    Value = optionsForField.TrueOptionName,
                                    Order = optionOrderCounter++,
                                }, def.title));
                            }

                        }
                    }
                }

                if (hoaInformationParentFieldsToCreate.Any())
                {
                    await _dbContext.DoorStepCustomFormSectionFields.AddRangeAsync(hoaInformationParentFieldsToCreate);
                    await _dbContext.SaveChangesAsync();
                    foreach (var field in hoaInformationParentFieldsToCreate) { parentFieldIdMap[field.Title] = field.Id; }
                }

                var optionsToSave = new List<DoorStepCustomFormCustomControlTypeValue>();
                foreach (var (option, binding) in hoaInformationOptionsToLink)
                {
                    if (parentFieldIdMap.TryGetValue(binding, out var fieldId))
                    {
                        option.SectionFieldId = fieldId;
                        optionsToSave.Add(option);
                    }
                }

                if (optionsToSave.Any())
                {
                    await _dbContext.DoorStepCustomFormCustomControlTypeValues.AddRangeAsync(optionsToSave);
                    await _dbContext.SaveChangesAsync();
                }

                var hoaInformationConditionalFieldsToCreate = new List<DoorStepCustomFormSectionConditionalField>();
                var hoaInformationConditionalOptionsToLink = new List<(DoorStepCustomFormSectionConditionalOption Option, string Title)>();


                var hoaInformationConditionalDefinitions = new (
                    string title,
                    string type,
                    bool isRequired,
                    string triggerBinding,
                    string triggerOptionValue,
                    int controlTypeId,
                    bool hasGlobalOptions)[]
                {
                    ("Name", DoorStepCustomControlTypeStrings.TextSingleLine, true, "Does the customer have an HOA?", ntpMappings.Where(x => x.Title == "Does the customer have an HOA?").Select(x => x.TrueOptionName).FirstOrDefault(), controlTypeIds.TextSingleLineId, false),
                    ("Phone", DoorStepCustomControlTypeStrings.Phone, true, "Does the customer have an HOA?", ntpMappings.Where(x => x.Title == "Does the customer have an HOA?").Select(x => x.TrueOptionName).FirstOrDefault(), controlTypeIds.PhoneId, false),
                    ("Email", DoorStepCustomControlTypeStrings.Email, false, "Does the customer have an HOA?", ntpMappings.Where(x => x.Title == "Does the customer have an HOA?").Select(x => x.TrueOptionName).FirstOrDefault(), controlTypeIds.EmailId, false),
                    ("HOA Address", DoorStepCustomControlTypeStrings.AddressControl, true, "Does the customer have an HOA?", ntpMappings.Where(x => x.Title == "Does the customer have an HOA?").Select(x => x.TrueOptionName).FirstOrDefault(), controlTypeIds.AddressId, false),
                };


                foreach (var def in hoaInformationConditionalDefinitions)
                {
                    if (!parentFieldIdMap.TryGetValue(def.triggerBinding, out int parentFieldId) || parentFieldId <= 0)
                    {
                        Console.WriteLine($"WARNING: Parent Field ID for {def.triggerBinding} is invalid or missing. Skipping conditional field {def.title}.");
                        continue;
                    }

                    var conditionalKey = GetConditionalKey(def.title, parentFieldId.ToString());
                    if (existingConditionalFieldMap.ContainsKey(conditionalKey)) continue;

                    var triggerOptionEntity = await _dbContext.DoorStepCustomFormCustomControlTypeValues
                        .FirstOrDefaultAsync(o =>
                            o.SectionFieldId == parentFieldId &&
                            o.Value == def.triggerOptionValue);

                    if (triggerOptionEntity == null)
                    {
                        throw new InvalidOperationException($"FATAL: Trigger Option '{def.triggerOptionValue}' not found for Parent {def.triggerBinding}. Check if the option was saved correctly.");
                    }

                    var newConditionalField = new DoorStepCustomFormSectionConditionalField
                    {
                        TenantId = tenantId,
                        Title = def.title,
                        CreationTime = DateTime.Now,
                        Order = orderCounter++,
                        FieldType = 3,
                        IsRequired = def.isRequired,
                        Type = def.type,
                        ParentId = triggerOptionEntity.Id,
                        CustomControlTypeId = def.controlTypeId
                    };
                    hoaInformationConditionalFieldsToCreate.Add(newConditionalField);
                    
                    if (ntpMappingsDict.TryGetValue(def.title, out var optionsForBinding))
                    {
                        int optionOrderCounter = 1;

                        if (!string.IsNullOrEmpty(optionsForBinding.FalseOptionName) || !string.IsNullOrWhiteSpace(optionsForBinding.FalseOptionName))
                        {

                            hoaInformationConditionalOptionsToLink.Add((new DoorStepCustomFormSectionConditionalOption
                            {
                                Value = optionsForBinding.FalseOptionName,
                                Order = optionOrderCounter++,
                                TenantId = tenantId,
                            }, def.title));
                        }

                        if (!string.IsNullOrEmpty(optionsForBinding.TrueOptionName) || !string.IsNullOrWhiteSpace(optionsForBinding.TrueOptionName))
                        {
                            string controlValue = optionsForBinding.TrueOptionName;

                            hoaInformationConditionalOptionsToLink.Add((new DoorStepCustomFormSectionConditionalOption
                            {
                                Value = optionsForBinding.TrueOptionName,
                                Order = optionOrderCounter++,
                                TenantId = tenantId,
                            }, def.title));
                        }
                    }
                }

                if (hoaInformationConditionalFieldsToCreate.Any())
                {
                    await _dbContext.DoorStepCustomFormSectionConditionalFields.AddRangeAsync(hoaInformationConditionalFieldsToCreate);
                    await _dbContext.SaveChangesAsync();                
                }
           }

            if (welcomeCallSection != null && welcomeCallSectionId != 0)
            {
                var existingWelcomeCallFields = await _dbContext.DoorStepCustomFormSectionFields
                    .Where(x => x.TenantId == tenantId && x.SectionId == welcomeCallSectionId && x.IsDeleted == false)
                    .ToListAsync();

                var existingParentFieldIds = existingWelcomeCallFields.Select(f => f.Id).ToList();
                var existingFieldMap = existingWelcomeCallFields.ToDictionary(f => f.Title, f => f.Id);
                var parentFieldIdMap = existingFieldMap.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

                var validParentOptionIds = await _dbContext.DoorStepCustomFormCustomControlTypeValues
                 .Where(o => existingParentFieldIds.Contains(o.SectionFieldId))
                 .Select(o => o.Id)
                 .ToListAsync();

                var existingConditionalFields = await _dbContext.DoorStepCustomFormSectionConditionalFields
                    .Include(x => x.ControlType)
                    .Where(x => x.TenantId == tenantId && validParentOptionIds.Contains(x.ParentId) && x.IsDeleted == false)
                    .ToListAsync();

                var existingConditionalFieldMap = existingConditionalFields.ToDictionary(f => GetConditionalKey(f.Title, f.ControlType.SectionFieldId.ToString()), f => f.Id);

                var welcomeCallParentFieldsToCreate = new List<DoorStepCustomFormSectionField>();
                var welcomeCallOptionsToLink = new List<(DoorStepCustomFormCustomControlTypeValue Option, string title)>();

                int orderCounter = existingWelcomeCallFields.Any() ? existingWelcomeCallFields.Max(f => f.Order) + 1 : 1;

                var hoaInformationParentDefinitions = new (string title, string type, int fieldType, bool isRequired, int controlTypeId, CustomFormDataBindingEnum? enumValue)[]
                {
                    ("Was the Welcome Call successfully completed with Callpilot?", DoorStepCustomControlTypeStrings.SelectButton, 3,  true, controlTypeIds.SelectButtonId, null),
                    ("Review Welcome Call", DoorStepCustomControlTypeStrings.Button, 3,  false, controlTypeIds.ButtonId, CustomFormDataBindingEnum.ReviewWelcomeCall),
                    ("Send To Sales Rep (Email)", DoorStepCustomControlTypeStrings.Button, 3,  false, controlTypeIds.ButtonId, CustomFormDataBindingEnum.SendSalesRepEmail),
                    ("Send To Homeowner (Email)", DoorStepCustomControlTypeStrings.Button, 3,  false, controlTypeIds.ButtonId, CustomFormDataBindingEnum.SendHomeOwnerEmail),
                    ("Send To Sales Rep (SMS)", DoorStepCustomControlTypeStrings.Button, 3,  false, controlTypeIds.ButtonId, CustomFormDataBindingEnum.SendSalesRepSms),
                    ("Send To Homeowner (SMS)", DoorStepCustomControlTypeStrings.Button, 3,  false, controlTypeIds.ButtonId, CustomFormDataBindingEnum.SendHomeOwnerSms),
                    ("Promise Made", DoorStepCustomControlTypeStrings.TextMultiLine, 3,  false, controlTypeIds.TextMultiLineId, CustomFormDataBindingEnum.PromisesMadeActivityNotes),
                    ("Save Note", DoorStepCustomControlTypeStrings.Button, 3,  false, controlTypeIds.ButtonId, CustomFormDataBindingEnum.PromisesMade),
                };

                foreach (var def in hoaInformationParentDefinitions)
                {
                    if (existingFieldMap.ContainsKey(def.title))
                    {
                        parentFieldIdMap[def.title] = existingFieldMap[def.title];
                        continue;
                    }

                    var newField = new DoorStepCustomFormSectionField
                    {
                        TenantId = tenantId,
                        SectionId = welcomeCallSectionId,
                        Title = def.title,
                        CreationTime = DateTime.Now,
                        Order = orderCounter++,
                        FieldType = def.fieldType,
                        IsRequired = def.isRequired,
                        Type = def.type,
                        CustomControlTypeId = def.controlTypeId,
                        DataBinding = def.enumValue.HasValue ? def.enumValue.Value : (CustomFormDataBindingEnum?)null

                    };
                    welcomeCallParentFieldsToCreate.Add(newField);

                    if ((def.type == DoorStepCustomControlTypeStrings.SelectButton || def.type == "Dropdown") && ntpMappingsDict.TryGetValue(def.title, out var optionsForField))
                    {
                        int optionOrderCounter = 1;

                        if (optionsForField != null)
                        {

                            if (!string.IsNullOrEmpty(optionsForField.FalseOptionName) && !string.IsNullOrWhiteSpace(optionsForField.FalseOptionName))
                            {
                                welcomeCallOptionsToLink.Add((new DoorStepCustomFormCustomControlTypeValue
                                {
                                    Value = optionsForField.FalseOptionName,
                                    Order = optionOrderCounter++,
                                }, def.title));
                            }

                            if (!string.IsNullOrWhiteSpace(optionsForField.TrueOptionName) && !string.IsNullOrEmpty(optionsForField.TrueOptionName))
                            {
                                welcomeCallOptionsToLink.Add((new DoorStepCustomFormCustomControlTypeValue
                                {
                                    Value = optionsForField.TrueOptionName,
                                    Order = optionOrderCounter++,
                                }, def.title));
                            }

                        }
                    }
                }

                if (welcomeCallParentFieldsToCreate.Any())
                {
                    await _dbContext.DoorStepCustomFormSectionFields.AddRangeAsync(welcomeCallParentFieldsToCreate);
                    await _dbContext.SaveChangesAsync();
                    foreach (var field in welcomeCallParentFieldsToCreate) { parentFieldIdMap[field.Title] = field.Id; }
                }

                var optionsToSave = new List<DoorStepCustomFormCustomControlTypeValue>();
                foreach (var (option, binding) in welcomeCallOptionsToLink)
                {
                    if (parentFieldIdMap.TryGetValue(binding, out var fieldId))
                    {
                        option.SectionFieldId = fieldId;
                        optionsToSave.Add(option);
                    }
                }

                if (optionsToSave.Any())
                {
                    await _dbContext.DoorStepCustomFormCustomControlTypeValues.AddRangeAsync(optionsToSave);
                    await _dbContext.SaveChangesAsync();
                }

                var welcomeCallConditionalFieldsToCreate = new List<DoorStepCustomFormSectionConditionalField>();
                var welcomeCallConditionalOptionsToLink = new List<(DoorStepCustomFormSectionConditionalOption Option, string Title)>();


                var welcomeCallConditionalDefinitions = new (
                    string title,
                    string type,
                    bool isRequired,
                    string triggerBinding,
                    string triggerOptionValue,
                    int controlTypeId,
                    bool hasGlobalOptions,
                    CustomFormDataBindingEnum enumValue)[]
                {
                    ("Conduct Welcome Call", DoorStepCustomControlTypeStrings.Button, false, "Was the Welcome Call successfully completed with Callpilot?", ntpMappings.Where(x => x.Title == "Was the Welcome Call successfully completed with Callpilot?").Select(x => x.FalseOptionName).FirstOrDefault(), controlTypeIds.ButtonId, false, CustomFormDataBindingEnum.ConductWelcomeCallByPhone),
               };


                foreach (var def in welcomeCallConditionalDefinitions)
                {
                    if (!parentFieldIdMap.TryGetValue(def.triggerBinding, out int parentFieldId) || parentFieldId <= 0)
                    {
                        Console.WriteLine($"WARNING: Parent Field ID for {def.triggerBinding} is invalid or missing. Skipping conditional field {def.title}.");
                        continue;
                    }

                    var conditionalKey = GetConditionalKey(def.title, parentFieldId.ToString());
                    if (existingConditionalFieldMap.ContainsKey(conditionalKey)) continue;

                    var triggerOptionEntity = await _dbContext.DoorStepCustomFormCustomControlTypeValues
                        .FirstOrDefaultAsync(o =>
                            o.SectionFieldId == parentFieldId &&
                            o.Value == def.triggerOptionValue);

                    if (triggerOptionEntity == null)
                    {
                        throw new InvalidOperationException($"FATAL: Trigger Option '{def.triggerOptionValue}' not found for Parent {def.triggerBinding}. Check if the option was saved correctly.");
                    }

                    var newConditionalField = new DoorStepCustomFormSectionConditionalField
                    {
                        TenantId = tenantId,
                        Title = def.title,
                        CreationTime = DateTime.Now,
                        Order = orderCounter++,
                        FieldType = 3,
                        IsRequired = def.isRequired,
                        Type = def.type,
                        ParentId = triggerOptionEntity.Id,
                        CustomControlTypeId = def.controlTypeId,
                        DataBinding = def.enumValue
                    };
                    welcomeCallConditionalFieldsToCreate.Add(newConditionalField);

                    if (ntpMappingsDict.TryGetValue(def.title, out var optionsForBinding))
                    {
                        int optionOrderCounter = 1;

                        if (!string.IsNullOrEmpty(optionsForBinding.FalseOptionName) || !string.IsNullOrWhiteSpace(optionsForBinding.FalseOptionName))
                        {

                            welcomeCallConditionalOptionsToLink.Add((new DoorStepCustomFormSectionConditionalOption
                            {
                                Value = optionsForBinding.FalseOptionName,
                                Order = optionOrderCounter++,
                                TenantId = tenantId,
                            }, def.title));
                        }

                        if (!string.IsNullOrEmpty(optionsForBinding.TrueOptionName) || !string.IsNullOrWhiteSpace(optionsForBinding.TrueOptionName))
                        {
                            string controlValue = optionsForBinding.TrueOptionName;

                            welcomeCallConditionalOptionsToLink.Add((new DoorStepCustomFormSectionConditionalOption
                            {
                                Value = optionsForBinding.TrueOptionName,
                                Order = optionOrderCounter++,
                                TenantId = tenantId,
                            }, def.title));
                        }
                    }
                }

                if (welcomeCallConditionalFieldsToCreate.Any())
                {
                    await _dbContext.DoorStepCustomFormSectionConditionalFields.AddRangeAsync(welcomeCallConditionalFieldsToCreate);
                    await _dbContext.SaveChangesAsync();
                }
            }

        }

        private string GetConditionalKey(string title, string triggerBinding)
        => $"{triggerBinding}::{title}";
    }
}
