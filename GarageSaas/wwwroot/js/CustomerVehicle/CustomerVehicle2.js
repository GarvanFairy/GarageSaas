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
    const selected = (selector) => {
        const option = $(`${selector} option:selected`);
        return {
            Value: (option.val() || "").trim(),
            Text: (option.text() || "").trim()
        };
    };

    const toNullableInt = (value) => {
        const parsed = parseInt(value, 10);
        return isNaN(parsed) ? null : parsed;
    };

    const vehicleId = toNullableInt($('input[name="Vehicle.Id"]').val()) || 0;

    const owner = selected("#ddlVehicleOwner");
    const make = selected("#ddlVehicleMake");
    const model = selected("#ddlVehicleModel");
    const fuel = selected("#ddlFuelType");
    const mileage = selected("#ddlMileage");
    const transmission = selected("#ddlTransmission");
    const year = selected("#ddlVehicleYears");
    const nctMonth = selected("#ddlNCTMonth");
    const nctYear = selected("#ddlNCTYear");
    const taxMonth = selected("#ddlTaxMonth");
    const taxYear = selected("#ddlTaxYear");

    const registration = ($("#txtRegistration").val() || "").trim();
    const isNewCustomer = $("#chkNewCustomer").is(":checked");

    const mileageDateIso = toIsoDate($("#txtDate").val()) || new Date().toISOString();

    const errors = [];

    if (!registration) errors.push("Registration is required.");
    if (!make.Value) errors.push("Make is required.");
    if (!model.Value) errors.push("Model is required.");
    if (!fuel.Value) errors.push("Fuel type is required.");
    if (!year.Value) errors.push("Vehicle year is required.");

    if (!isNewCustomer && !owner.Value) {
        errors.push("Owner is required.");
    }

    let newCustomerModel = null;

    if (isNewCustomer) {
        newCustomerModel = {
            Forename: ($("#txtNewCustomerForename").val() || "").trim(),
            Surname: ($("#txtNewCustomerSurname").val() || "").trim(),
            MobileNumber: ($("#txtNewCustomerMobile").val() || "").trim(),
            EmailAddress: ($("#txtNewCustomerEMail").val() || "").trim()
        };

        if (!newCustomerModel.Forename && !newCustomerModel.Surname) {
            errors.push("New customer first name or surname is required.");
        }
    }

    if (errors.length > 0) {
        showStatus("danger", errors.join("<br/>"));
        return;
    }

    const vehicleAndCustomers = {
        Vehicle: {
            Id: vehicleId,
            VehicleRegistration: registration,

            VehicleMakeId: toNullableInt(make.Value),
            VehicleModelId: toNullableInt(model.Value),
            VehicleFuelTypeId: toNullableInt(fuel.Value),
            VehicleMileageId: toNullableInt(mileage.Value),
            VehicleTransmissionId: toNullableInt(transmission.Value),
            VehicleYearId: toNullableInt(year.Value),

            VehicleNCTDue: `${nctMonth.Text} ${nctYear.Text}`.trim(),
            VehicleTaxDue: `${taxMonth.Text} ${taxYear.Text}`.trim(),
            VehicleMileageDate: mileageDateIso,
            GarageOwned: false
        },

        AddNewCustomer: isNewCustomer,

        GarageVehicleOwnerListItem: isNewCustomer ? null : owner,
        VehicleMakeListItem: make,
        VehicleModelListItem: model,
        FuelTypeListItem: fuel,
        VehicleMileageListItem: mileage,
        VehicleYearListItem: year,
        TransmissionListItem: transmission,

        NCTMonthListItem: nctMonth,
        NCTYearListItem: nctYear,
        TaxMonthListItem: taxMonth,
        TaxYearListItem: taxYear,

        NewCustomer: newCustomerModel
    };

    console.log("Posting:", JSON.stringify(vehicleAndCustomers));

    $("#btnSavejs").prop("disabled", true);

    $.ajax({
        url: "/CustomerVehicle/AddUpdateCustomerVehicle",
        type: "POST",
        contentType: "application/json; charset=utf-8",
        dataType: "json",
        processData: false,
        data: JSON.stringify(vehicleAndCustomers),
        headers: {
            "RequestVerificationToken": $('input[name="__RequestVerificationToken"]').val()
        }
    })
        .done(function () {
            showStatus("success", "Saved successfully.");
            window.location = "/CustomerVehicle/CustomerVehicleList";
        })
        .fail(function (xhr) {
            showStatus("danger", xhr.responseText || "Save failed.");
        })
        .always(function () {
            $("#btnSavejs").prop("disabled", false);
        });
}

function SubmitVehicleCustomers_notUsed() {
    const vehicleId = parseInt($('input[name="Vehicle.Id"]').val() || "0", 10);

    const isNewCustomer = $('#chkNewCustomer').is(':checked');

    const vehicleAndCustomers = {
        Vehicle: {
            Id: vehicleId,
            VehicleRegistration: ($("#txtRegistration").val() || "").trim(),
            VehicleMakeId: parseInt($("#ddlVehicleMake").val() || "0", 10),
            VehicleModelId: parseInt($("#ddlVehicleModel").val() || "0", 10),
            VehicleFuelTypeId: parseInt($("#ddlFuelType").val() || "0", 10),
            VehicleMileageId: parseInt($("#ddlMileage").val() || "0", 10),
            VehicleTransmissionId: parseInt($("#ddlTransmission").val() || "0", 10),
            VehicleYearId: parseInt($("#ddlVehicleYears").val() || "0", 10),
            VehicleMileageDate: toIsoDate($("#txtDate").val()) || new Date().toISOString(),
            VehicleNCTDue: `${$("#ddlNCTMonth").val()} ${$("#ddlNCTYear").val()}`,
            VehicleTaxDue: `${$("#ddlTaxMonth").val()} ${$("#ddlTaxYear").val()}`,
            GarageOwned: false
        },

        AddNewCustomer: isNewCustomer,

        GarageVehicleOwnerListItem: isNewCustomer ? null : {
            Value: $("#ddlVehicleOwner").val(),
            Text: $("#ddlVehicleOwner option:selected").text()
        },

        VehicleMakeListItem: {
            Value: $("#ddlVehicleMake").val(),
            Text: $("#ddlVehicleMake option:selected").text()
        },

        VehicleModelListItem: {
            Value: $("#ddlVehicleModel").val(),
            Text: $("#ddlVehicleModel option:selected").text()
        },

        FuelTypeListItem: {
            Value: $("#ddlFuelType").val(),
            Text: $("#ddlFuelType option:selected").text()
        },

        VehicleMileageListItem: {
            Value: $("#ddlMileage").val(),
            Text: $("#ddlMileage option:selected").text()
        },

        VehicleYearListItem: {
            Value: $("#ddlVehicleYears").val(),
            Text: $("#ddlVehicleYears option:selected").text()
        },

        TransmissionListItem: {
            Value: $("#ddlTransmission").val(),
            Text: $("#ddlTransmission option:selected").text()
        },

        NCTMonthListItem: {
            Value: $("#ddlNCTMonth").val(),
            Text: $("#ddlNCTMonth option:selected").text()
        },

        NCTYearListItem: {
            Value: $("#ddlNCTYear").val(),
            Text: $("#ddlNCTYear option:selected").text()
        },

        TaxMonthListItem: {
            Value: $("#ddlTaxMonth").val(),
            Text: $("#ddlTaxMonth option:selected").text()
        },

        TaxYearListItem: {
            Value: $("#ddlTaxYear").val(),
            Text: $("#ddlTaxYear option:selected").text()
        },

        NewCustomer: isNewCustomer ? {
            Forename: ($("#txtNewCustomerForename").val() || "").trim(),
            Surname: ($("#txtNewCustomerSurname").val() || "").trim(),
            MobileNumber: ($("#txtNewCustomerMobile").val() || "").trim(),
            EmailAddress: ($("#txtNewCustomerEMail").val() || "").trim()
        } : null
    };

    console.log("Posting vehicleAndCustomers:", vehicleAndCustomers);
    console.log(JSON.stringify(vehicleAndCustomers));

    $("#btnSavejs").prop("disabled", true);

    $.ajax({
        url: "/CustomerVehicle/AddUpdateCustomerVehicle",
        type: "POST",
        contentType: "application/json; charset=utf-8",
        dataType: "json",
        processData: false,
        data: JSON.stringify(vehicleAndCustomers),
        headers: {
            "RequestVerificationToken": $('input[name="__RequestVerificationToken"]').val()
        }
    })
        .done(function () {
            showStatus("success", "Saved successfully.");
            window.location = "/CustomerVehicle/CustomerVehicleList";
        })
        .fail(function (xhr) {
            showStatus("danger", xhr.responseText || "Save failed.");
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