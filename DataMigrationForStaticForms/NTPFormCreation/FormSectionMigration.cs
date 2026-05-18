using Core365.DoorStep;
using Core365.DoorStep.CustomForm;
using Core365.EntityFrameworkCore;
using Core365.RenewableEnergy.Account;
using Core365.RenewableEnergy.CustomForm;
using Microsoft.EntityFrameworkCore;

namespace DataMigrationForStaticForms.NTPFormCreation
{           
    public class FormSectionMigration
    {
        private readonly Core365DbContext _core365DbContext;

        public FormSectionMigration(Core365DbContext core365DbContext)
        {
            _core365DbContext = core365DbContext;
        }

        private async Task MigrateNTPSectionsToCustomFormSections(int tenantId)
        {
            var type = CustomFormIncludesWithTypes.NTP;

            var ntpFormId = await _core365DbContext
                .DoorStepCustomFormIncludeWithMappings
                .Where(x => x.IncludeWithId == type && !x.IsDeleted && x.TenantId == tenantId)
                .OrderByDescending(x => x.CreationTime)
                .Select(x => x.FormId)
                .FirstOrDefaultAsync();

            int? latestFormId = ntpFormId;

            if (!latestFormId.HasValue || latestFormId.Value == 0)
            {
                Console.WriteLine("[SETUP] No relevant NTP form definition found (no Form ID retrieved). Aborting migration.");
                return;
            }

            var formSections = await _core365DbContext
                .DoorStepCustomFormSections
                .Where(x => x.FormId.HasValue && x.TenantId == tenantId && latestFormId == x.FormId && !x.IsDeleted)
                .ToListAsync();

            var ntpRecords = await _core365DbContext
                .RenewableEnergyAccountNtps
                .Where(x => x.TenantId == tenantId && !x.IsDeleted)
                .ToListAsync();

            var newSectionsToInsert = new List<RenewableEnergyCustomFormSection>();
            var newFormsToInsert = new List<RenewableEnergyCustomForm>();

            if (ntpRecords == null || ntpRecords.Count == 0)
            {
                Console.WriteLine("[INFO] No NTP records found to migrate.");
                return;
            }

            foreach (var ntp in ntpRecords)
            {
                // General Section
                int? generalSectionId = GetSectionId(formSections, NTPSections.General);
                if (generalSectionId.HasValue && generalSectionId != 0)
                {
                    newSectionsToInsert.AddRange(CreateSectionVerificationRecords(
                        ntp,
                        NTPSections.General,
                        ntp.IsGeneralReviewComplete,
                        ntp.GeneralReviewCompletedUserId,
                        ntp.GeneralReviewCompleteDate,
                        latestFormId.Value,
                        generalSectionId.Value,
                        ntp.CreatorUserId,
                        ntp.CreationTime
                    ));
                }

                // Secondary Customer Section
                int? secondaryCustomerSectionId = GetSectionId(formSections, NTPSections.SecondaryCustomer);
                if (secondaryCustomerSectionId.HasValue && secondaryCustomerSectionId != 0)
                {
                    newSectionsToInsert.AddRange(CreateSectionVerificationRecords(
                        ntp,
                        NTPSections.SecondaryCustomer,
                        ntp.IsSecondaryCustomerReviewComplete,
                        ntp.SecondaryCustomerReviewCompletedUserId,
                        ntp.SecondaryCustomerReviewCompleteDate,
                        latestFormId.Value,
                        secondaryCustomerSectionId.Value,
                        ntp.CreatorUserId,
                        ntp.CreationTime
                    ));
                }

                // Financing Section
                int? financingSectionId = GetSectionId(formSections, NTPSections.Financing);
                if (financingSectionId.HasValue && financingSectionId != 0)
                {
                    newSectionsToInsert.AddRange(CreateSectionVerificationRecords(
                        ntp,
                        NTPSections.Financing,
                        ntp.IsFinancingReviewComplete,
                        ntp.FinanancingReviewCompletedUserId,
                        ntp.FinancingReviewCompleteDate,
                        latestFormId.Value,
                        financingSectionId.Value,
                        ntp.CreatorUserId,
                        ntp.CreationTime
                    ));
                }

                // Equipment Section
                int? equipmentSectionId = GetSectionId(formSections, NTPSections.Equipment);
                if (equipmentSectionId.HasValue && equipmentSectionId != 0)
                {
                    newSectionsToInsert.AddRange(CreateSectionVerificationRecords(
                        ntp,
                        NTPSections.Equipment,
                        ntp.IsEquipmentReviewComplete,
                        ntp.EquipmentReviewCompletedUserId,
                        ntp.EquipmentReviewCompleteDate,
                        latestFormId.Value,
                        equipmentSectionId.Value,
                        ntp.CreatorUserId,
                        ntp.CreationTime
                    ));
                }

                // Adders Section
                int? addersSectionId = GetSectionId(formSections, NTPSections.Adders);
                if (addersSectionId.HasValue && addersSectionId != 0)
                {
                    newSectionsToInsert.AddRange(CreateSectionVerificationRecords(
                        ntp,
                        NTPSections.Adders,
                        ntp.IsAdderReviewComplete,
                        ntp.AdderReviewCompletedUserId,
                        ntp.AdderReviewCompleteDate,
                        latestFormId.Value,
                        addersSectionId.Value,
                        ntp.CreatorUserId,
                        ntp.CreationTime
                    ));
                }

                // Utility Bill Section
                int? utilityBillSectionId = GetSectionId(formSections, NTPSections.UtilityBill);
                if (utilityBillSectionId.HasValue && utilityBillSectionId != 0)
                {
                    newSectionsToInsert.AddRange(CreateSectionVerificationRecords(
                        ntp,
                        NTPSections.UtilityBill,
                        ntp.IsUtilityReviewComplete,
                        ntp.UtilityReviewCompletedUserId,
                        ntp.UtilityReviewCompleteDate,
                        latestFormId.Value,
                        utilityBillSectionId.Value,
                        ntp.CreatorUserId,
                        ntp.CreationTime
                    ));
                }

                // HOA Information Section
                int? hoaSectionId = GetSectionId(formSections, NTPSections.HOAInformation);
                if (hoaSectionId.HasValue && hoaSectionId != 0)
                {
                    newSectionsToInsert.AddRange(CreateSectionVerificationRecords(
                        ntp,
                        NTPSections.HOAInformation,
                        ntp.IsHOAReviewComplete,
                        ntp.HOAReviewCompletedUserId,
                        ntp.HOAReviewCompleteDate,
                        latestFormId.Value,
                        hoaSectionId.Value,
                        ntp.CreatorUserId,
                        ntp.CreationTime
                    ));
                }

                // Welcome Call Section
                int? welcomeCallSectionId = GetSectionId(formSections, NTPSections.WelcomeCall);
                if (welcomeCallSectionId.HasValue && welcomeCallSectionId != 0)
                {
                    newSectionsToInsert.AddRange(CreateSectionVerificationRecords(
                        ntp,
                        NTPSections.WelcomeCall,
                        ntp.IsWelcomeCallReviewComplete,
                        ntp.WelcomeCallReviewCompletedUserId,
                        ntp.WelcomeCallReviewCompleteDate,
                        latestFormId.Value,
                        welcomeCallSectionId.Value,
                        ntp.CreatorUserId,
                        ntp.CreationTime
                    ));
                }

                newFormsToInsert.Add(CreateFormVerificationRecord(ntp, latestFormId.Value));
            }

            var existingKeys = await _core365DbContext.RenewableEnergyCustomFormSections
                .Where(x => x.FormId == latestFormId.Value && x.IsDeleted == false && x.TenantId == tenantId)
                .Select(x => new { x.AccountId, x.SectionId })
                .ToListAsync();

            var existingKeySet = new HashSet<(int AccountId, int SectionId)>(
                   existingKeys.Select(r => (r.AccountId, r.SectionId))
                );

            var sectionsToActuallyInsert = new List<RenewableEnergyCustomFormSection>();

            foreach (var newSection in newSectionsToInsert)
            {
                var key = (newSection.AccountId, newSection.SectionId);

                if (existingKeySet.Contains(key))
                {
                    Console.WriteLine($"[DUPLICATE SKIP] Skipping record for AccountId: {newSection.AccountId}, SectionId: {newSection.SectionId}. Record already exists in target table.");
                    continue;
                }

                sectionsToActuallyInsert.Add(newSection);
                existingKeySet.Add(key);
            }

            _core365DbContext.RenewableEnergyCustomFormSections.AddRange(sectionsToActuallyInsert);

            var existingFormKeys = await _core365DbContext.RenewableEnergyCustomForms
                .Where(x => x.FormId == latestFormId.Value && x.IsDeleted == false)
                .Select(x => x.AccountId)
                .ToListAsync();

            var existingFormKeySet = new HashSet<int>(existingFormKeys);

            var formsToActuallyInsert = new List<RenewableEnergyCustomForm>();

            foreach (var newForm in newFormsToInsert)
            {
                if (existingFormKeySet.Contains(newForm.AccountId))
                {
                    Console.WriteLine($"[DUPLICATE SKIP] Skipping FORM record for AccountId: {newForm.AccountId}. Record already exists in target table.");
                    continue;
                }
                formsToActuallyInsert.Add(newForm);
                existingFormKeySet.Add(newForm.AccountId);
            }

            _core365DbContext.RenewableEnergyCustomForms.AddRange(formsToActuallyInsert);

            await _core365DbContext.SaveChangesAsync();
        }

        private int? GetSectionId(List<DoorStepCustomFormSection> formSections, string sectionTitle)
        {
            return formSections
                .Where(x => string.Equals(x.Title?.Trim(), sectionTitle, StringComparison.OrdinalIgnoreCase))
                .Select(x => x.Id)
                .FirstOrDefault();
        }

        private IEnumerable<RenewableEnergyCustomFormSection> CreateSectionVerificationRecords(
           RenewableEnergyAccountNtp sourceNtp,
           string sectionTitle,
           bool isCompleted,
           long? completedBySourceUserId,
           DateTime? completedDate,
           int targetFormId,
           int targetSectionId,
           long? creatorUserId,
           DateTime creationTime)
        {
            var verifiedUtc = completedDate.HasValue
                 ? DateTime.SpecifyKind(completedDate.Value, DateTimeKind.Utc)
                    : (DateTime?)null;

            var creationUtc = DateTime.SpecifyKind(creationTime, DateTimeKind.Utc);

            yield return new RenewableEnergyCustomFormSection
            {
                AccountId = sourceNtp.AccountId,
                FormId = targetFormId,
                SectionId = targetSectionId,
                TenantId = sourceNtp.TenantId,
                IsVerified = isCompleted,
                VerifiedDate = verifiedUtc,
                VerifiedByUserId = completedBySourceUserId,
                CreationTime = creationUtc,
                CreatorUserId = creatorUserId,
            };
        }

        private RenewableEnergyCustomForm CreateFormVerificationRecord(
           RenewableEnergyAccountNtp sourceNtp,
           int targetFormId)
        {
            bool isFormCompleted = sourceNtp.Progress == 100;

            DateTime? completedDate = isFormCompleted ? DateTime.UtcNow : null;

            return new RenewableEnergyCustomForm
            {
                AccountId = sourceNtp.AccountId,
                FormId = targetFormId,
                TenantId = sourceNtp.TenantId,
                IsCompleted = isFormCompleted,
                CompletedDate = completedDate,
                CompletedByUserId = null,
            };
        }
    }
}
