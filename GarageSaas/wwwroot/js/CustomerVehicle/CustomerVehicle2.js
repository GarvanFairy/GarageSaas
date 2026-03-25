// wwwroot/js/CustomerVehicle/CustomerVehicle2.js

function SubmitVehicleCustomers() {

    // --- read form fields (aligned to updated view) ---
    const vehicleId = parseInt($('input[name="Vehicle.Id"]').val() || "0");

    const registration = ($("#txtRegistration").val() || "").trim();

    const ownerId = $("#ddlVehicleOwner option:selected").val();
    const ownerText = $("#ddlVehicleOwner option:selected").text();

    const makeId = $("#ddlVehicleMake option:selected").val(); //$("#ddlVehicleMake option:selected").val();
    const makeText = $("#ddlVehicleMake option:selected").text();

    const modelId = $("#ddlVehicleModel option:selected").val();
    const modelText = $("#ddlVehicleModel option:selected").text();

    const fuelId = $("#ddlFuelType option:selected").val();
    const fuelText = $("#ddlFuelType option:selected").text();

    const mileageId = $("#ddlMileage option:selected").val();
    const mileageText = $("#ddlMileage option:selected").text();

    const transmissionVal = $("#ddlTransmission option:selected").val();
    const transmissionText = $("#ddlTransmission option:selected").text();

    const yearId = $("#ddlVehicleYears option:selected").val();
    const yearText = $("#ddlVehicleYears option:selected").text();

    var mileageDateIso = new Date();
    var getIsoDate = toIsoDate($("#txtDate").val());
    if (getIsoDate != null) {
        mileageDateIso = getIsoDate;
    }

    console.log("txtDate val " + $("#txtDate").val());
    console.log("Mileage Date ISO: " + mileageDateIso);

    const nctMonth = $("#ddlNCTMonth option:selected").val();
    const nctMonthText = $("#ddlNCTMonth option:selected").text();
    const nctYear = $("#ddlNCTYear option:selected").val();
    const nctYearText = $("#ddlNCTYear option:selected").text();

    const taxMonth = $("#ddlTaxMonth option:selected").val();
    const taxMonthText = $("#ddlTaxMonth option:selected").text();
    const taxYear = $("#ddlTaxYear option:selected").val();
    const taxYearText = $("#ddlTaxYear option:selected").text();

    // --- quick client-side validation ---
    const errors = [];
    if (!registration) errors.push("Registration is required.");
    //if (!ownerId) errors.push("Owner is required.");
    if (!makeId) errors.push("Make is required.");
    if (!modelId) errors.push("Model is required.");
    if (!fuelId) errors.push("Fuel type is required.");
    if (!yearId) errors.push("Vehicle year is required.");

    if (errors.length > 0) {
        showStatus("danger", errors.join("<br/>"));
        return;
    }


    // --- build payload matching your C# VehicleAndCustomers ---
    // Controller uses: VehicleMakeListItem.Text, VehicleModelListItem.Text, FuelTypeListItem.Text, etc.
    // Controller builds:
    //  - VehicleNCTDue = NCTMonthListItem.Text + " " + NCTYearListItem.Text
    //  - VehicleTaxDue = TaxMonthListItem.Text + " " + TaxYearListItem.Text
    const VehicleViewModel = {
        Id: vehicleId,
        VehicleRegistration: registration,

        // These are still OK to send (and may be used in update branch),
        // but your controller primarily uses SelectListItem.Text values.
        VehicleMake: makeText,
        VehicleModel: modelText,
        VehicleFuelType: fuelText,
        VehicleMileage: mileageText,
        VehicleYear: yearText,
        VehicleTransmission: transmissionText,

        // keep these consistent with controller behavior
        VehicleNCTDue: (nctMonthText && nctYearText) ? (nctMonthText + " " + nctYearText) : "",
        VehicleTaxDue: (taxMonthText && taxYearText) ? (taxMonthText + " " + taxYearText) : "",

        VehicleMileageDate: mileageDateIso, 
        GarageOwned: false
    };

    var NewCustomerModel = null; // default to null if not new customer
    var isNewCustomer = $('#chkNewCustomer').is(':checked');
    if (isNewCustomer) {

        NewCustomerModel = {
            Forename: $('#txtNewCustomerForename').val(),
            Surname: $('#txtNewCustomerSurname').val(),
            Mobile: $('#txtNewCustomerMobile').val(),
            EmailAddress: $('#txtNewCustomerEmail').val()
        };
    }

    const VehicleAndCustomers = {
        Vehicle: VehicleViewModel,

        GarageVehicleOwnerListItem: { Value: ownerId, Text: ownerText },
        VehicleMakeListItem: { Value: makeId, Text: makeText },
        VehicleModelListItem: { Value: modelId, Text: modelText },
        FuelTypeListItem: { Value: fuelId, Text: fuelText },
        VehicleMileageListItem: { Value: mileageId, Text: mileageText },
        VehicleYearListItem: { Value: yearId, Text: yearText },
        TransmissionListItem: { Value: transmissionVal, Text: transmissionText },

        NCTMonthListItem: { Value: nctMonth, Text: nctMonthText },
        NCTYearListItem: { Value: nctYear, Text: nctYearText },
        TaxMonthListItem: { Value: taxMonth, Text: taxMonthText },
        TaxYearListItem: { Value: taxYear, Text: taxYearText },
        AddNewCustomer: isNewCustomer,
        NewCustomer: NewCustomerModel
    };

    // --- disable button to prevent double clicks ---
    $("#btnSavejs").prop("disabled", true);

    var VehicleCustomerVM = JSON.stringify(VehicleAndCustomers);

    console.log(VehicleCustomerVM);


    $.ajax({
        contentType: "application/json; charset=utf-8",
        type: "POST",
        processData: false,
        url: "/CustomerVehicle/AddUpdateCustomerVehicle",
        data: VehicleCustomerVM,
        dataType: "json"
    })
        .done(function (data) {
            // Your controller returns Json("Success") (string) on success
            showStatus("success", "Saved successfully.");
            window.location = "/CustomerVehicle/CustomerVehicleList";
        })
        .fail(function (xhr) {
            const msg = xhr.responseText || "Save failed.";
            showStatus("danger", msg);
        })
        .always(function () {
            $("#btnSavejs").prop("disabled", false);
        });

}

// Make -> Model dependent dropdown
$('#ddlVehicleMake').on('change', function () {
    var makeId = $(this).val();
    if (!makeId) {
        $('#ddlVehicleModel').empty().append('<option value="">-- Select model --</option>');
        return;
    }

    $.ajax({
        url: "/CustomerVehicle/GetModelsByMake",
        type: 'GET',
        data: { makeId: makeId },
        success: function (data) {
            var modelDropdown = $('#ddlVehicleModel');
            modelDropdown.empty();
            modelDropdown.append('<option value="">-- Select model --</option>');
            console.log("Data: " + data);
            //console.log("model: " + model);
            // $.each(data, function (index, model) {
            //     modelDropdown.append('<option value="' + model.id + '">' + model.model + '</option>');
            // });


            $.each(data, function (index, item) {
                modelDropdown.append(
                    $('<option>', {
                        value: item.value,
                        text: item.text
                    })
                );
            });

            // If editing and current model exists, try to reselect by text
            var currentModelText = '@(Model?.Vehicle?.VehicleModel ?? "")';
            if (currentModelText) {
                $("#ddlVehicleModel option").filter(function () {
                    return $(this).text() === currentModelText;
                }).prop("selected", true);
            }
        },
        error: function () {
            showError('Error retrieving models');
        }
    });
});

// Optional: trigger change on load if editing and make already selected
if ($('#ddlVehicleMake').val()) {
    // only load models if list is empty / placeholder
    // comment this out if you always pre-load models server-side
    // $('#ddlVehicleMake').trigger('change');
}

function showError(msg) {
    $("#statusArea").html(`
                                            <div class="alert alert-danger alert-dismissible fade show shadow-sm" role="alert">
                                                <i class="bi bi-exclamation-triangle me-1"></i> ${msg}
                                                <button type="button" class="btn-close" data-bs-dismiss="alert"></button>
                                            </div>
                                        `);
}

function toIsoDate(dateStr) {
    if (!dateStr) return null;

    // expecting dd/MM/yyyy
    const parts = dateStr.split('/');
    if (parts.length !== 3) return null;

    const day = parseInt(parts[0], 10);
    const month = parseInt(parts[1], 10) - 1; // JS months are 0-based
    const year = parseInt(parts[2], 10);

    const date = new Date(year, month, day);

    // ISO string without timezone offset issues
    return date.toISOString();
}



// Bootstrap alert helper (works with the updated view's #statusArea)
function showStatus(type, htmlMessage) {
    const icon = (type === "success") ? "bi-check-circle" : "bi-exclamation-triangle";
    $("#statusArea").html(`
        <div class="alert alert-${type} alert-dismissible fade show shadow-sm" role="alert">
            <i class="bi ${icon} me-1"></i> ${htmlMessage}
            <button type="button" class="btn-close" data-bs-dismiss="alert"></button>
        </div>
    `);
}
