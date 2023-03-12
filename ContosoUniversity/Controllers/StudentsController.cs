using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using ContosoUniversity.Data;
using ContosoUniversity.Models;
using ContosoUniversity.Models.SchoolViewModels;

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ContosoUniversity.Controllers
{
    public class StudentsController : Controller
    {
        private readonly SchoolContext _context;

        public StudentsController(SchoolContext context)
        {
            _context = context;
        }

        // GET: Students
        public async Task<IActionResult> Index(
            string sortOrder,
            string currentFilter,
            string searchString,
            int? pageNumber,
            int pageSize = 3)
        {
            ViewData["CurrentSort"] = sortOrder;
            ViewData["NameSortParm"] = string.IsNullOrEmpty(sortOrder) ? "name_desc" : "";
            ViewData["DateSortParm"] = sortOrder == "Date" ? "date_desc" : "Date";
            ViewData["PageSize"] = pageSize;

            if (searchString != null)
            {
                pageNumber = 1;
            }
            else
            {
                searchString = currentFilter;
            }

            ViewData["CurrentFilter"] = searchString;

            IQueryable<Student> students = from s in _context.Students
                                           select s;
            if (!string.IsNullOrEmpty(searchString))
            {
                students = students.Where(s => s.LastName.Contains(searchString)
                                       || s.FirstMidName.Contains(searchString));
            }
            switch (sortOrder)
            {
                case "name_desc":
                    students = students.OrderByDescending(s => s.LastName);
                    break;
                case "Date":
                    students = students.OrderBy(s => s.EnrollmentDate);
                    break;
                case "date_desc":
                    students = students.OrderByDescending(s => s.EnrollmentDate);
                    break;
                default:
                    students = students.OrderBy(s => s.LastName);
                    break;
            }

            return View(await PaginatedList<Student>.CreateAsync(students.AsNoTracking(), pageNumber ?? 1, pageSize));
        }

        // GET: Students/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            Student student = await _context.Students
                 .Include(s => s.Enrollments)
                     .ThenInclude(e => e.Course)
                 .AsNoTracking()
                 .FirstOrDefaultAsync(m => m.ID == id);

            return student == null ? NotFound() : (IActionResult)View(student);
        }

        // GET: Students/Create
        public IActionResult Create()
        {
            // This sets the default value for the EnrollmentDate field.
            return View(new Student());
        }

        private void PopulateAssignedCourseData(Student student)
        {
            DbSet<Course> allCourses = _context.Courses;
            HashSet<int> studentCourses = new HashSet<int>(student.Enrollments.Select(c => c.CourseID));
            List<AssignedCourseData> viewModel = new List<AssignedCourseData>();
            foreach (Course course in allCourses)
            {
                viewModel.Add(new AssignedCourseData
                {
                    CourseID = course.CourseID,
                    Title = course.Title,
                    Assigned = studentCourses.Contains(course.CourseID)
                });
            }
            ViewData["Courses"] = viewModel;
        }

        // POST: Students/Create
        // To protect from overposting attacks, please enable the specific properties you want to bind to, for
        // more details see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(
            [Bind("EnrollmentDate,FirstMidName,LastName")] Student student)
        {
            try
            {
                if (ModelState.IsValid)
                {
                    _context.Add(student);
                    await _context.SaveChangesAsync();
                    return RedirectToAction(nameof(Index));
                }
            }
            catch (DbUpdateException /* ex */)
            {
                //Log the error (uncomment ex variable name and write a log.
                ModelState.AddModelError("", "Unable to save changes. " +
                    "Try again, and if the problem persists " +
                    "see your system administrator.");
            }
            return View(student);
        }

        // GET: Students/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            Student student = await _context.Students
                .Include(s => s.Enrollments)
                .FirstAsync(s => s.ID == id);

            PopulateAssignedCourseData(student);
            return student == null ? NotFound() : View(student);
        }

        // POST: Students/Edit/5
        // To protect from overposting attacks, please enable the specific properties you want to bind to, for
        // more details see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost, ActionName("Edit")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditPost(int? id, int[] selectedCourses)
        {
            Student studentToUpdate = await _context.Students
                .Include(s => s.Enrollments)
                    .ThenInclude(s => s.Course)
                .FirstOrDefaultAsync(s => s.ID == id);

            if (await TryUpdateModelAsync(
                studentToUpdate,
                "",
                s => s.FirstMidName, s => s.LastName, s => s.EnrollmentDate))
            {
                UpdateStudentEnrollments(selectedCourses, studentToUpdate);
                try
                {
                    await _context.SaveChangesAsync();
                    return RedirectToAction(nameof(Index));
                }
                catch (DbUpdateException /* ex */)
                {
                    //Log the error (uncomment ex variable name and write a log.)
                    ModelState.AddModelError("", "Unable to save changes. " +
                        "Try again, and if the problem persists, " +
                        "see your system administrator.");
                }
            }


            PopulateAssignedCourseData(studentToUpdate);
            return View(studentToUpdate);
        }

        private void UpdateStudentEnrollments(int[] selectedCourses, Student studentToUpdate)
        {
            if (selectedCourses == null)
            {
                studentToUpdate.Enrollments = new List<Enrollment>();
                return;
            }

            HashSet<int> selectedCoursesHS = new(selectedCourses);
            HashSet<int> instructorCourses = new HashSet<int>
                (studentToUpdate.Enrollments.Select(c => c.Course.CourseID));
            foreach (Course course in _context.Courses)
            {
                if (selectedCoursesHS.Contains(course.CourseID))
                {
                    if (!instructorCourses.Contains(course.CourseID))
                    {
                        studentToUpdate.Enrollments.Add(new Enrollment { StudentID = studentToUpdate.ID, CourseID = course.CourseID });
                    }
                }
                else
                {
                    if (instructorCourses.Contains(course.CourseID))
                    {
                        Enrollment courseToRemove = studentToUpdate.Enrollments.FirstOrDefault(i => i.CourseID == course.CourseID);
                        _context.Remove(courseToRemove);
                    }
                }
            }
        }


        // GET: Students/Delete/5
        public async Task<IActionResult> Delete(int? id, bool? saveChangesError = false)
        {
            if (id == null)
            {
                return NotFound();
            }

            Student student = await _context.Students
                .AsNoTracking()
                .FirstOrDefaultAsync(m => m.ID == id);
            if (student == null)
            {
                return NotFound();
            }

            if (saveChangesError.GetValueOrDefault())
            {
                ViewData["ErrorMessage"] =
                    "Delete failed. Try again, and if the problem persists " +
                    "see your system administrator.";
            }

            return View(student);
        }
        // POST: Students/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            Student student = await _context.Students.FindAsync(id);
            if (student == null)
            {
                return RedirectToAction(nameof(Index));
            }

            try
            {
                _context.Students.Remove(student);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            catch (DbUpdateException /* ex */)
            {
                //Log the error (uncomment ex variable name and write a log.)
                return RedirectToAction(nameof(Delete), new { id, saveChangesError = true });
            }
        }

        private bool StudentExists(int id)
        {
            return _context.Students.Any(e => e.ID == id);
        }
    }
}
