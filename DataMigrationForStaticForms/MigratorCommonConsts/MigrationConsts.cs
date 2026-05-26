using System;
using System.Collections.Generic;
using System.Text;

namespace DataMigrationForStaticForms.MigratorCommonConsts
{
    public class MigrationConsts
    {
        public static class DoorStepCustomControlTypeStrings
        {
            // Text Group
            public const string Email = "Email";
            public const string Phone = "Phone";
            public const string Text = "Text"; // Note: The provided code also has a TextSingleLine and TextMultiLine
            public const string Number = "Number";
            public const string Currency = "Currency";
            public const string DisplayText = "DisplayText";
            public const string TextSingleLine = "TextSingleLine";
            public const string TextMultiLine = "TextMultiLine";
            public const string SSNInput = "SSN";
            

            // Option Group
            public const string Toggle = "Toggle";
            public const string Dropdown = "Dropdown";
            public const string MultiDropdown = "MultiDropdown";
            public const string Checkbox = "Checkbox";
            public const string RadioButtons = "RadioButtons";
            public const string SelectButton = "SelectButton";

            // Component Group
            public const string Date = "Date";
            public const string Time = "Time";
            public const string Image = "Image";
            public const string Button = "Button";
            public const string RoofControl = "RoofControl";
            public const string AdderControl = "Adders";
            public const string AddressControl = "Address";



        }

    }
}
