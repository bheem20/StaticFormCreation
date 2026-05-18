using Core365.DoorStep.CustomForm;
using Core365.DoorStep.CustomFormGlobalControls;
using Core365.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace DataMigrationForStaticForms.GlobalOptions
{
    public class GlobalOptionConfigurtion
    {
        private readonly Core365DbContext _core365DbContext;

        public GlobalOptionConfigurtion(Core365DbContext core365DbContext)
        {
            _core365DbContext = core365DbContext;
        }

        public async Task MigrateAsync(int tenantId, List<BooleanMapConfigDto> mappings, bool isUpdate = false)
        {
            if (mappings == null || mappings.Count == 0)
            {
                Console.WriteLine("[INFO] No mappings provided for migration.");
                return;
            }

            // Load existing options once
            var existingOptions = await _core365DbContext.CustomFormGlobalFieldOptions
                .Where(x => x.TenantId == tenantId && x.IsDeleted == false)
                .ToListAsync();

            var existingSet = new HashSet<(CustomFormDataBindingEnum Binding, string Name)>(
                existingOptions.Select(x => (x.CustomFormDataBinding, x.Name))
            );

            var optionsToInsert = new List<CustomFormGlobalFieldOption>();

            if (isUpdate == false)
            {
                foreach (var mapping in mappings)
                {
                    if (!existingSet.Contains((mapping.Binding, mapping.TrueOptionName)) && !string.IsNullOrWhiteSpace(mapping.TrueOptionName))
                    {
                        optionsToInsert.Add(new CustomFormGlobalFieldOption
                        {
                            TenantId = tenantId,
                            Name = mapping.TrueOptionName,
                            CustomFormDataBinding = mapping.Binding,
                            IsTrue = true,
                            IsActive = true
                        });

                        existingSet.Add((mapping.Binding, mapping.TrueOptionName));
                    }

                    // False option
                    if (mapping.HasExplicitFalse && !string.IsNullOrWhiteSpace(mapping.FalseOptionName) && !string.IsNullOrWhiteSpace(mapping.FalseOptionName))
                    {
                        if (!existingSet.Contains((mapping.Binding, mapping.FalseOptionName)))
                        {
                            optionsToInsert.Add(new CustomFormGlobalFieldOption
                            {
                                TenantId = tenantId,
                                Name = mapping.FalseOptionName,
                                CustomFormDataBinding = mapping.Binding,
                                IsTrue = false,
                                IsActive = true
                            });

                            existingSet.Add((mapping.Binding, mapping.FalseOptionName));
                        }
                    }

                }

                if (optionsToInsert.Count > 0)
                {
                    _core365DbContext.CustomFormGlobalFieldOptions.AddRange(optionsToInsert);
                    await _core365DbContext.SaveChangesAsync();
                    Console.WriteLine($"[INFO] Inserted {optionsToInsert.Count} new CustomFormGlobalFieldOption records.");
                }
                else
                {
                    Console.WriteLine("[INFO] No new CustomFormGlobalFieldOption records to insert.");
                }
            }
            else if (isUpdate)
            {
                foreach (var option in existingOptions)
                {
                    var mapping = mappings.Where(x => x.Binding == option.CustomFormDataBinding).FirstOrDefault();

                    if (mapping != null)
                    {
                        if (option.Name == mapping.TrueOptionName && (option.IsTrue == null || !option.IsTrue.Value))
                        {
                            option.IsTrue = true;
                        }

                        if (option.Name == mapping.FalseOptionName && (option.IsTrue == null || option.IsTrue.Value))
                        {
                            option.IsTrue = false;
                        }
                    }
                }

                await _core365DbContext.SaveChangesAsync();
            }
        }

        public async Task MigratePostCadFieldOptionsAsync(int tenantId, List<BooleanMapConfigDto> mappings, bool isUpdate = false)
        {
            if (mappings == null || mappings.Count == 0)
            {
                Console.WriteLine("[INFO] No mappings provided for migration.");
                return;
            }

            // Load existing options once
            var existingOptions = await _core365DbContext.CustomFormGlobalFieldOptions
                .Where(x => x.TenantId == tenantId && x.IsDeleted == false)
                .ToListAsync();

            var existingSet = new HashSet<(CustomFormDataBindingEnum Binding, string Name)>(
                existingOptions.Select(x => (x.CustomFormDataBinding, x.Name))
            );

            var optionsToInsert = new List<CustomFormGlobalFieldOption>();

            if (isUpdate == false)
            {
                foreach (var mapping in mappings)
                {
                    // True option
                    if (!existingSet.Contains((mapping.Binding, mapping.TrueOptionName)) && !string.IsNullOrWhiteSpace(mapping.TrueOptionName))
                    {
                        optionsToInsert.Add(new CustomFormGlobalFieldOption
                        {
                            TenantId = tenantId,
                            Name = mapping.TrueOptionName,
                            CustomFormDataBinding = mapping.Binding,
                            IsTrue = true,
                            IsActive = true
                        });

                        existingSet.Add((mapping.Binding, mapping.TrueOptionName));
                    }

                    // False option
                    if (mapping.HasExplicitFalse && !existingSet.Contains((mapping.Binding, mapping.FalseOptionName)) && !string.IsNullOrWhiteSpace(mapping.FalseOptionName) && !string.IsNullOrWhiteSpace(mapping.FalseOptionName))
                    {
                        if (!existingSet.Contains((mapping.Binding, mapping.FalseOptionName)))
                        {
                            optionsToInsert.Add(new CustomFormGlobalFieldOption
                            {
                                TenantId = tenantId,
                                Name = mapping.FalseOptionName,
                                CustomFormDataBinding = mapping.Binding,
                                IsTrue = false,
                                IsActive = true
                            });

                            existingSet.Add((mapping.Binding, mapping.FalseOptionName));
                        }
                    }

                }

                if (optionsToInsert.Count > 0)
                {
                    _core365DbContext.CustomFormGlobalFieldOptions.AddRange(optionsToInsert);
                    await _core365DbContext.SaveChangesAsync();
                    Console.WriteLine($"[INFO] Inserted {optionsToInsert.Count} new CustomFormGlobalFieldOption records.");
                }
                else
                {
                    Console.WriteLine("[INFO] No new CustomFormGlobalFieldOption records to insert.");
                }
            }
            else if (isUpdate)
            {
                foreach (var option in existingOptions)
                {
                    var mapping = mappings.Where(x => x.Binding == option.CustomFormDataBinding).FirstOrDefault();

                    if (mapping != null)
                    {
                        if (option.Name == mapping.TrueOptionName && (option.IsTrue == null || !option.IsTrue.Value))
                        {
                            option.IsTrue = true;
                        }

                        if (option.Name == mapping.FalseOptionName && (option.IsTrue == null || option.IsTrue.Value))
                        {
                            option.IsTrue = false;
                        }
                    }
                }

                await _core365DbContext.SaveChangesAsync();
            }
        }

    }
}
