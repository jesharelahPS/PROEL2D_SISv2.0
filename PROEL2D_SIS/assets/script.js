// -----------------------------
// UI FUNCTIONS
// -----------------------------
const navigate = (page) => window.location.href = page;
const toggleSidebar = () => document.getElementById("sidebar").classList.toggle("collapsed");
const toggleDropdown = () => document.getElementById("profileDropdown").classList.toggle("show");
const logout = () => {
    if (window.chrome && window.chrome.webview) {
        window.chrome.webview.postMessage(JSON.stringify({ type: "logout" }));
    }
};

window.addEventListener("click", e => {
    const dd = document.getElementById("profileDropdown");
    if (!e.target.matches(".profile-pic")) dd.classList.remove("show");
});

function showPage(pageId) {
    // hide all sections
    document.querySelectorAll(".main-content section").forEach(sec =>
        sec.classList.add("hidden")
    );
    document.getElementById(pageId).classList.remove("hidden");

    // remove old active highlight
    document.querySelectorAll(".sidebar .menu-item").forEach(item =>
        item.classList.remove("active")
    );

    // add highlight to the clicked button
    const activeBtn = document.querySelector(`[onclick="showPage('${pageId}')"]`);
    if (activeBtn) activeBtn.classList.add("active");
}








//admin dashboard------------------------------------------------

let charts = {};


// -----------------------------
// CHART
// -----------------------------
function createChart(ctxId, type, title, labels = [], data = [], extraOptions = {}) {
    return new Chart(document.getElementById(ctxId).getContext("2d"), {
        type,
        data: {
            labels,
            datasets: [{
                label: title,
                data,
                backgroundColor: type === "pie" ? ["#3c0008", "#ffd700"] : "#ffd700",
                borderColor: "#3c0008",
                borderWidth: 1,
                fill: type === "line",
                tension: type === "line" ? 0.3 : 0
            }]
        },
        options: {
            responsive: true,
            maintainAspectRatio: false,
            plugins: {
                legend: { position: "bottom" },
                title: { display: true, text: title, font: { family: "Cinzel, serif", size: 18 } }
            },
            ...extraOptions
        }
    });
}

function initCharts() {
    charts.pie = createChart("pieChart", "pie", "Active vs Inactive Students", ["Active", "Inactive"], [0, 0]);
    charts.bar = createChart("barChart", "bar", "Courses per Teacher", [], [], { scales: { y: { beginAtZero: true, ticks: { stepSize: 1 } } } });
    charts.line = createChart("lineChart", "line", "Student Growth Over Time", [], [], { scales: { y: { beginAtZero: true } } });
}

initCharts();

// -----------------------------
// MESSAGE HANDLER
// -----------------------------
window.chrome.webview.addEventListener("message", event => {
    let msg;
    try { msg = JSON.parse(event.data); } catch { return; }

    switch (msg.type) {
        case "setUser":
            document.querySelector(".username").textContent = "Welcome, " + msg.username;
            break;

        case "dashboardStats":
            const d = msg.data;

            // Update widgets
            document.getElementById("totalStudents").textContent = d.TotalStudents;
            document.getElementById("totalTeachers").textContent = d.TotalTeachers;
            document.getElementById("totalCourses").textContent = d.TotalCourses;

            // Pie chart (active vs inactive)
            charts.pie.data.datasets[0].data = [d.ActiveStudents, d.InactiveStudents];
            charts.pie.update();

            // Bar chart (courses per teacher)
            charts.bar.data.labels = Object.keys(d.TeacherCourses);
            charts.bar.data.datasets[0].data = Object.values(d.TeacherCourses);
            charts.bar.update();

            // Line chart (growth over months)
            if (d.StudentGrowth) {
                charts.line.data.labels = d.StudentGrowth.map(x => x.Month);
                charts.line.data.datasets[0].data = d.StudentGrowth.map(x => x.Count);
                charts.line.update();
            }
            break;
    }
});






const selectAll = document.getElementById('selectAll');
const bulkDeleteBtn = document.querySelector('.delete-btn');
const studentTableBody = document.getElementById('studentTableBody');

// Function to update bulk delete button
function updateBulkDeleteBtn() {
    const anyChecked = studentTableBody.querySelectorAll('.row-select:checked').length > 0;
    if (anyChecked) {
        bulkDeleteBtn.classList.add('active');
    } else {
        bulkDeleteBtn.classList.remove('active');
    }
}

// Select all checkbox
selectAll.addEventListener('change', () => {
    studentTableBody.querySelectorAll('.row-select').forEach(cb => {
        cb.checked = selectAll.checked;
    });
    updateBulkDeleteBtn();
});

// Delegate event for row checkboxes (works for dynamic rows too)
studentTableBody.addEventListener('change', (e) => {
    if (e.target.classList.contains('row-select')) {
        updateBulkDeleteBtn();

        // If any checkbox is unchecked, uncheck "selectAll"
        if (!e.target.checked) {
            selectAll.checked = false;
        } else {
            // If all checkboxes are checked, check "selectAll"
            const allChecked = studentTableBody.querySelectorAll('.row-select').length ===
                studentTableBody.querySelectorAll('.row-select:checked').length;
            selectAll.checked = allChecked;
        }
    }
});



/* ========== CRUD (EDIT / DELETE / ADD) ========== */

function editStudent(btn) {
    const row = btn.closest("tr");
    const firstName = row.cells[1].innerText;
    const lastName = row.cells[2].innerText;
    alert("Edit student: " + firstName + " " + lastName);
}

function deleteStudent(btn) {
    const row = btn.closest("tr");
    const firstName = row.cells[1].innerText;
    const lastName = row.cells[2].innerText;
    if (confirm("Delete student: " + firstName + " " + lastName + "?")) {
        row.remove();
    }
}

/* ========== MODAL OPEN / CLOSE ========== */

function addStudent() {
    const modal = document.getElementById("addStudentModal");
    modal.style.display = "flex";

    // auto generate ID
    const randomId = "S" + Math.floor(100000 + Math.random() * 900000);
    document.getElementById("studentId").value = randomId;

    // auto fill enrollment date with today
    const today = new Date().toISOString().slice(0, 10);
    document.getElementById("enrollmentDate").value = today;
}

function closeAddModal() {
    document.getElementById("addStudentModal").style.display = "none";
}

/* ========== PHOTO PREVIEW ========== */

const photoInput = document.getElementById("photo");
const photoPreview = document.getElementById("photoPreview");

photoInput.addEventListener("change", () => {
    const file = photoInput.files[0];
    if (file) {
        const reader = new FileReader();
        reader.onload = (e) => (photoPreview.src = e.target.result);
        reader.readAsDataURL(file);
    }
});

/* ========== AUTO CALCULATE AGE ========== */

const dobInput = document.getElementById("dob");
const ageInput = document.getElementById("age");

dobInput.addEventListener("change", function () {
    const dob = new Date(this.value);
    const diff = Date.now() - dob.getTime();
    const age = Math.floor(diff / (1000 * 60 * 60 * 24 * 365.25));
    ageInput.value = age >= 0 ? age : "";
});

/* ========== FORM SUBMIT ========== */

document.getElementById("addStudentForm").addEventListener("submit", function (e) {
    e.preventDefault();

    const phonePattern = /^\d*$/;
    if (!phonePattern.test(document.getElementById("phone").value)) {
        alert("Phone must contain only numbers.");
        return;
    }

    alert("Student added! (Demo)");
    closeAddModal();
    this.reset();
    ageInput.value = "";
    photoPreview.src = "img/profile_placeholder.png";
});

/* ========== TOGGLE PASSWORD VISIBILITY ========== */

function togglePW(id, el) {
    const field = document.getElementById(id);
    if (field.type === "password") {
        field.type = "text";
        el.classList.remove("fa-eye-slash");
        el.classList.add("fa-eye");
    } else {
        field.type = "password";
        el.classList.remove("fa-eye");
        el.classList.add("fa-eye-slash");
    }
}

document.querySelectorAll("th.sortable").forEach((header, index) => {
    let asc = true;
    header.addEventListener("click", () => {
        const tbody = document.getElementById("studentTableBody");
        const rows = Array.from(tbody.querySelectorAll("tr"));

        rows.sort((a, b) => {
            let cellA = a.cells[index].innerText.toLowerCase();
            let cellB = b.cells[index].innerText.toLowerCase();

            // If numeric, compare as numbers
            if (!isNaN(cellA) && !isNaN(cellB)) {
                cellA = Number(cellA);
                cellB = Number(cellB);
            }

            return asc ? (cellA > cellB ? 1 : -1) : (cellA < cellB ? 1 : -1);
        });

        tbody.innerHTML = "";
        rows.forEach(row => tbody.appendChild(row));

        asc = !asc;
    });
});


flatpickr("#dob", {
    dateFormat: "Y-m-d",
    allowInput: true
});

flatpickr("#enrollmentDate", {
    dateFormat: "Y-m-d",
    allowInput: true
});


window.chrome.webview.addEventListener("message", event => {
    let msg;
    try {
        msg = JSON.parse(event.data);
    } catch (e) {
        console.error("Invalid message:", e);
        return;
    }

    if (msg.type === "setUser") {
        document.querySelector(".username").textContent = "Welcome, " + msg.username;
    }

    if (msg.type === "studentData") {
        const tbody = document.getElementById("studentTableBody");
        tbody.innerHTML = "";

        msg.data.forEach(student => {
            const row = document.createElement("tr");
            row.innerHTML = `
                <td><input type="checkbox" class="row-select"></td>
                <td>${student.StudentId}</td>
                <td>${student.FirstName}</td>
                <td>${student.LastName}</td>
                <td>${student.DateOfBirth}</td>
                <td>${calculateAge(student.DateOfBirth)}</td>
                <td>${student.Gender}</td>
                <td>${student.Email}</td>
                <td>${student.Phone}</td>
                <td>${student.Address}</td>
                <td>${student.EnrollmentDate}</td>
                <td>${student.Status}</td>
                <td>
                    <button class="edit" onclick="editStudent(this)">Edit</button>
                    <button class="delete" onclick="deleteStudent(this)">Delete</button>
                </td>
            `;
            tbody.appendChild(row);
        });
    }
});

function calculateAge(dobStr) {
    const dob = new Date(dobStr);
    const diff = Date.now() - dob.getTime();
    return Math.floor(diff / (1000 * 60 * 60 * 24 * 365.25));
}




//teacher----------------------------------------------------------------------------















// -------------------- LOGS FUNCTIONS --------------------

// Search logs
const searchLogInput = document.getElementById("searchLogInput");
const logsTableBody = document.getElementById("logsTableBody");

function filterLogs() {
    const filter = searchLogInput.value.toLowerCase();
    const dateFilter = document.getElementById("filterDate").value;

    [...logsTableBody.rows].forEach(row => {
        const action = row.cells[2].innerText.toLowerCase();
        const date = row.cells[1].innerText;

        let matchesSearch = action.includes(filter);
        let matchesDate = !dateFilter || date === dateFilter;

        row.style.display = matchesSearch && matchesDate ? "" : "none";
    });
}

searchLogInput.addEventListener("keyup", filterLogs);
document.getElementById("filterDate").addEventListener("change", filterLogs);

// Delete a single log
function deleteLog(btn) {
    if (confirm("Delete this log entry?")) {
        btn.closest("tr").remove();
    }
}

// Delete selected logs
function deleteSelectedLogs() {
    const selected = document.querySelectorAll("#logsTableBody .row-select:checked");
    if (selected.length && confirm("Delete selected log entries?")) {
        selected.forEach(cb => cb.closest("tr").remove());
    }
}

// Select all checkbox
document.getElementById("selectAllLogs").addEventListener("change", function () {
    document.querySelectorAll("#logsTableBody .row-select").forEach(cb => cb.checked = this.checked);
});

// Sort by date
document.querySelector("th.sortable").addEventListener("click", () => {
    const rows = Array.from(logsTableBody.querySelectorAll("tr"));
    let asc = document.querySelector("th.sortable").classList.toggle("asc");

    rows.sort((a, b) => {
        const dateA = new Date(a.cells[1].innerText);
        const dateB = new Date(b.cells[1].innerText);
        return asc ? dateA - dateB : dateB - dateA;
    });

    logsTableBody.innerHTML = "";
    rows.forEach(row => logsTableBody.appendChild(row));
});














//archives--------------------------------------
function showArchive(tabId) {
    document.querySelectorAll(".archive-tab").forEach(tab => tab.classList.add("hidden"));
    document.getElementById(tabId).classList.remove("hidden");
}

