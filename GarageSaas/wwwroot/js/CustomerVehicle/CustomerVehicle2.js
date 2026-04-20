// wwwroot/js/CustomerVehicle/CustomerVehicle2.js

$(function () {
    // Datepicker should already be initialized in the Razor view, but safe to leave DOM-ready handlers here.

    $('#ddlVehicleMake').on('change', function () {
        var makeId = $(this).val();
        var modelDropdown = $('#ddlVehicleModel');

        modelDropdown.empty().append('<option value="">-- Select model --</option>');

        if (!makeId) {
            return;
        }

        $.ajax({
            url: "/CustomerVehicle/GetModelsByMake",
            type: "GET",
            data: { makeId: makeId },
            success: function (data) {
                $.each(data, function (index, item) {
                    modelDropdown.append(
                        $('<option>', {
                            value: item.value,
                            text: item.text
                        })
                    );
                });

                // optional edit-mode reselection:
                var currentModelId = modelDropdown.data('current-model-id');
                if (currentModelId) {
                    modelDropdown.val(currentModelId);
                }
            },
            error: function () {
                showError("Error retrieving models");
            }
        });
    });
});

function SubmitVehicleCustomers() {
    const vehicleId = parseInt($('input[name="Vehicle.Id"]').val() || "0", 10);

    const registration = ($("#txtRegistration").val() || "").trim();

    const ownerId = $("#ddlVehicleOwner option:selected").val();
    const ownerText = $("#ddlVehicleOwner option:selected").text();

    const makeId = $("#ddlVehicleMake option:selected").val();
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

    const mileageDateIso = toIsoDate($("#txtDate").val()) || new Date().toISOString();

    const nctMonth = $("#ddlNCTMonth option:selected").val();
    const nctMonthText = $("#ddlNCTMonth option:selected").text();
    const nctYear = $("#ddlNCTYear option:selected").val();
    const nctYearText = $("#ddlNCTYear option:selected").text();

    const taxMonth = $("#ddlTaxMonth option:selected").val();
    const taxMonthText = $("#ddlTaxMonth option:selected").text();
    const taxYear = $("#ddlTaxYear option:selected").val();
    const taxYearText = $("#ddlTaxYear option:selected").text();

    const isNewCustomer = $('#chkNewCustomer').is(':checked');

    const errors = [];

    if (!registration) errors.push("Registration is required.");
    if (!makeId) errors.push("Make is required.");
    if (!modelId) errors.push("Model is required.");
    if (!fuelId) errors.push("Fuel type is required.");
    if (!yearId) errors.push("Vehicle year is required.");

    if (!isNewCustomer && !ownerId) {
        errors.push("Owner is required.");
    }

    if (isNewCustomer) {
        const newForename = ($('#txtNewCustomerForename').val() || '').trim();
        const newSurname = ($('#txtNewCustomerSurname').val() || '').trim();

        if (!newForename && !newSurname) {
            errors.push("New customer first name or surname is required.");
        }
    }

    if (errors.length > 0) {
        showStatus("danger", errors.join("<br/>"));
        return;
    }

    const vehicleViewModel = {
        Id: vehicleId,
        VehicleRegistration: registration,
        VehicleMake: makeText,
        VehicleModel: modelText,
        VehicleFuelType: fuelText,
        VehicleMileage: mileageText,
        VehicleYear: yearText,
        VehicleTransmission: transmissionText,
        VehicleNCTDue: (nctMonthText && nctYearText) ? (nctMonthText + " " + nctYearText) : "",
        VehicleTaxDue: (taxMonthText && taxYearText) ? (taxMonthText + " " + taxYearText) : "",
        VehicleMileageDate: mileageDateIso,
        GarageOwned: false
    };

    let newCustomerModel = null;

    if (isNewCustomer) {
        newCustomerModel = {
            Forename: ($('#txtNewCustomerForename').val() || '').trim(),
            Surname: ($('#txtNewCustomerSurname').val() || '').trim(),
            MobileNumber: ($('#txtNewCustomerMobile').val() || '').trim(),
            EmailAddress: ($('#txtNewCustomerEMail').val() || '').trim()
        };
    }

    const vehicleAndCustomers = {
        Vehicle: vehicleViewModel,
        AddNewCustomer: isNewCustomer,
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

        NewCustomer: newCustomerModel
    };

    $("#btnSavejs").prop("disabled", true);

    $.ajax({
        url: "/CustomerVehicle/AddUpdateCustomerVehicle",
        type: "POST",
        contentType: "application/json; charset=utf-8",
        dataType: "json",
        processData: false,
        data: JSON.stringify(vehicleAndCustomers)
    })
        .done(function () {
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

function toIsoDate(dateStr) {
    if (!dateStr) return null;

    const parts = dateStr.split('/');
    if (parts.length !== 3) return null;

    const day = parseInt(parts[0], 10);
    const month = parseInt(parts[1], 10) - 1;
    const year = parseInt(parts[2], 10);

    const date = new Date(year, month, day);

    if (isNaN(date.getTime())) return null;

    return date.toISOString();
}

function showError(msg) {
    $("#statusArea").html(`
        <div class="alert alert-danger alert-dismissible fade show shadow-sm" role="alert">
            <i class="bi bi-exclamation-triangle me-1"></i> ${msg}
            <button type="button" class="btn-close" data-bs-dismiss="alert"></button>
        </div>
    `);
}

function showStatus(type, htmlMessage) {
    const icon = (type === "success") ? "bi-check-circle" : "bi-exclamation-triangle";

    $("#statusArea").html(`
        <div class="alert alert-${type} alert-dismissible fade show shadow-sm" role="alert">
            <i class="bi ${icon} me-1"></i> ${htmlMessage}
            <button type="button" class="btn-close" data-bs-dismiss="alert"></button>
        </div>
    `);
}