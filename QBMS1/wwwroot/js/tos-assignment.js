$(document).ready(function () {

    function resetDropdown(selector, text) {

        $(selector)
            .empty()
            .append(
                $('<option></option>')
                    .val('')
                    .text(text)
            )
            .prop('disabled', true);
    }

    function loadDropdown(
        url,
        targetSelector,
        placeholder) {

        $.ajax({
            url: url,
            type: 'GET',
            dataType: 'json',

            success: function (data) {

                resetDropdown(
                    targetSelector,
                    placeholder);

                $.each(
                    data,
                    function (index, item) {

                        $(targetSelector)
                            .append(
                                $('<option></option>')
                                    .val(item.id)
                                    .text(item.name)
                            );
                    });

                $(targetSelector)
                    .prop('disabled', false);
            },
            error: function (xhr) {
                console.log("Dropdown Load Error", xhr.status, xhr.responseText);
                alert("Unable to Load the Dropdown Data.");
            }
        });
    }


    $('#ddlExam').change(function () {

        var examId = $(this).val();

        resetDropdown(
            '#ddlPaper',
            'Select Paper');

        resetDropdown(
            '#ddlSection',
            'Select Section');

        resetDropdown(
            '#ddlSubsection',
            'Select SubSection');

        resetDropdown(
            '#ddlTopic',
            'Select Topic');

        if (!examId) {
            return;
        }

        var url = window.qbmsUrls.getPapers + '?examId=' + encodeURIComponent(examId);
        loadDropdown(
            url,
            '#ddlPaper',
            'Select Paper');
    });


    $('#ddlPaper').change(function () {

        var paperId = $(this).val();

        resetDropdown(
            '#ddlSection',
            'Select Section');

        resetDropdown(
            '#ddlSubsection',
            'Select SubSection');

        resetDropdown(
            '#ddlTopic',
            'Select Topic');

        if (!paperId) {
            return;
        }
        var url = window.qbmsUrls.getSections + '?paperId=' + encodeURIComponent(paperId);
        loadDropdown(
            url,
            '#ddlSection',
            'Select Section');
    });


    $('#ddlSection').change(function () {

        var sectionId = $(this).val();

        resetDropdown(
            '#ddlSubsection',
            'Select SubSection');

        resetDropdown(
            '#ddlTopic',
            'Select Topic');

        if (!sectionId) {
            return;
        }
        var url = window.qbmsUrls.getSubsections + '?sectionId=' + encodeURIComponent(sectionId);
        loadDropdown(
            url,
            '#ddlSubsection',
            'Select SubSection');
    });


    $('#ddlSubsection').change(function () {

        var subsectionId = $(this).val();

        resetDropdown(
            '#ddlTopic',
            'Select Topic');

        if (!subsectionId) {
            return;
        }
        var url = window.qbmsUrls.getTopics + '?subsectionId=' + encodeURIComponent(subsectionId);
        loadDropdown(
            url,
            '#ddlTopic',
            'Select Topic');
    });


    // Which hierarchy fields apply to which level (Exam is always needed, so there is no "Exam" level).
    var LEVEL_RANK = { PAPER: 1, SECTION: 2, SUBSECTION: 3, TOPIC: 4 };

    function applyLevelFields() {

        var rank = LEVEL_RANK[$('#ddlAssignmentLevel').val()] || 0;

        $('.scope-field').each(function () {

            var show = rank >= parseInt($(this).attr('data-min-rank'), 10);

            $(this).prop('hidden', !show);

            if (!show) {
                // a field that does not apply must not leave a value behind
                $(this).find('select').val('');
            }
        });

        $('#scopeHint').prop('hidden', rank > 0);
    }

    // value of a hierarchy dropdown, or null when the chosen level does not use it
    function scopeValue(selector) {

        var field = $(selector).closest('.scope-field');

        if (field.length && field.prop('hidden')) {
            return null;
        }

        return $(selector).val() ? parseInt($(selector).val()) : null;
    }

    $('#ddlAssignmentLevel').change(function () {

        applyLevelFields();
        updateAssignmentDescription();
    });

    applyLevelFields();


    $('#ddlExam, #ddlPaper, #ddlSection, #ddlSubsection, #ddlTopic')
        .change(function () {

            updateAssignmentDescription();
        });


    function updateAssignmentDescription() {

        var level =
            $('#ddlAssignmentLevel').val();

        var text = '';

        if (level === 'PAPER') {

            text =
                'All Sections, SubSections and Topics '
                + 'under the selected Paper will be '
                + 'assigned to the same person.';

        }
        else if (level === 'SECTION') {

            text =
                'All SubSections and Topics under the '
                + 'selected Section will be assigned '
                + 'to the same person.';

        }
        else if (level === 'SUBSECTION') {

            text =
                'All Topics under the selected '
                + 'SubSection will be assigned to '
                + 'the same person.';

        }
        else if (level === 'TOPIC') {

            text =
                'Only the selected Topic will be '
                + 'assigned.';

        }
        else {

            text =
                'Select the hierarchy and '
                + 'assignment level.';
        }

        $('#assignmentDescription')
            .text(text);
    }
    $('#btnAssign').click(function () {
        var level = $('#ddlAssignmentLevel').val();

        if (!level) {
            $('#assignmentMessage')
                .text('Select the assignment level first.')
                .removeClass('success-message')
                .addClass('error-message');
            return;
        }

        var request = {
            examId: scopeValue('#ddlExam'),
            paperId: scopeValue('#ddlPaper'),
            sectionId: scopeValue('#ddlSection'),
            subsectionId: scopeValue('#ddlSubsection'),
            topicId: scopeValue('#ddlTopic'),
            assignmentLevel: level,
            setterUserId: $('#ddlSetter').val() ? parseInt($('#ddlSetter').val()) : null,
            vetterUserId: $('#ddlVetter').val() ? parseInt($('#ddlVetter').val()) : null,
            multiplicationFactor: parseFloat($('#txtFactor').val())
        };
        $.ajax({
            url: window.qbmsUrls.createAssignment,
            type: 'POST',
            contentType: 'application/json;charset=utf-8',
            data: JSON.stringify(request),
            success: function (response) {
                if (response.success) {
                    $('#assignmentMessage')
                        .text(
                            response.message + ' Assignment ID: ' + response.assignmentId
                        )
                        .removeClass('error-message')
                        .addClass('success-message');
                }
                else {
                    $('#assignmentMessage')
                        .text(
                            'Unable to create assignment.'
                        )
                        .removeClass('success-message')
                        .addClass('error-message');
                }
            },
            error: function (xhr) {
                console.log(xhr.status);
                console.log(xhr.responseText);
                $('#assignmentMessage')
                    .text('Unable to create assignment.')
                    .removeClass('success-message')
                    .addClass('error-message');
            }
        });
    });
});