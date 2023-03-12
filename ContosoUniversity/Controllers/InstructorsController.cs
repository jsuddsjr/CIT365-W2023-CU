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
    public class InstructorsController : Controller
    {
        private readonly SchoolContext _context;

        public InstructorsController(SchoolContext context)
        {
            _context = context;
        }

        // GET: Instructors
        public async Task<IActionResult> Index(int? id, int? courseID)
        {
            InstructorIndexData viewModel = new()
            {
                Instructors = await _context.Instructors
                    .Include(i => i.OfficeAssignment)
                    .Include(i => i.CourseAssignments)
                        .ThenInclude(i => i.Course)
                    .OrderBy(i => i.LastName)
                        .ThenBy(i => i.FirstMidName)
                    .AsNoTracking()
                    .ToListAsync()
            };

            if (id != null)
            {
                ViewData["InstructorID"] = id.Value;
                viewModel.Courses = await _context.CourseAssignments
                    .Include(c => c.Course)
                        .ThenInclude(c => c.Department)
                    .Where(c => c.InstructorID == id.Value)
                    .Select(c => c.Course)
                    .ToListAsync();
            }

            if (courseID != null)
            {
                ViewData["CourseID"] = courseID.Value;
                viewModel.Enrollments = (await _context.Courses
                    .Include(c => c.Enrollments)
                        .ThenInclude(e => e.Student)
                    .SingleAsync(c => c.CourseID == courseID.Value))
                    .Enrollments;
            }

            return View(viewModel);
        }

        // GET: Instructors/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            Instructor instructor = await _context.Instructors
                .Include(i => i.OfficeAssignment)
                .Include(i => i.CourseAssignments)
                    .ThenInclude(c => c.Course)
                .AsNoTracking()
                .FirstOrDefaultAsync(m => m.ID == id);
            return instructor == null ? NotFound() : (IActionResult)View(instructor);
        }

        // GET: Instructors/Create
        public IActionResult Create()
        {
            Instructor instructor = new()
            {
                CourseAssignments = new List<CourseAssignment>()
            };
            PopulateAssignedCourseData(instructor);
            return View(instructor);
        }

        // POST: Instructors/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("FirstMidName,HireDate,LastName,OfficeAssignment")] Instructor instructor, string[] selectedCourses)
        {
            if (selectedCourses != null)
            {
                instructor.CourseAssignments = new List<CourseAssignment>();
                foreach (string course in selectedCourses)
                {
                    CourseAssignment courseToAdd = new() { InstructorID = instructor.ID, CourseID = int.Parse(course) };
                    instructor.CourseAssignments.Add(courseToAdd);
                }
            }

            if (ModelState.IsValid)
            {
                _context.Add(instructor);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }

            PopulateAssignedCourseData(instructor);
            return View(instructor);
        }

        // GET: Instructors/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            Instructor instructor = await _context.Instructors
                .Include(i => i.OfficeAssignment)
                .Include(i => i.CourseAssignments)
                    .ThenInclude(i => i.Course)
                .AsNoTracking()
                .FirstOrDefaultAsync(m => m.ID == id);

            if (instructor == null)
            {
                return NotFound();
            }

            PopulateAssignedCourseData(instructor);
            return View(instructor);
        }

        private void PopulateAssignedCourseData(Instructor instructor)
        {
            DbSet<Course> allCourses = _context.Courses;
            HashSet<int> instructorCourses = new(instructor.CourseAssignments.Select(c => c.CourseID));
            List<AssignedCourseData> viewModel = new();
            foreach (Course course in allCourses)
            {
                viewModel.Add(new AssignedCourseData
                {
                    CourseID = course.CourseID,
                    Title = course.Title,
                    Assigned = instructorCourses.Contains(course.CourseID)
                });
            }
            ViewData["Courses"] = viewModel;
        }

        // POST: Instructors/Edit/5
        // To protect from overposting attacks, please enable the specific properties you want to bind to, for
        // more details see http://go.microsoft.com/fwlink/?LinkId=317598.

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int? id, int[] selectedCourses)
        {
            if (id == null)
            {
                return NotFound();
            }

            Instructor instructorToUpdate = await _context.Instructors
                .Include(i => i.OfficeAssignment)
                .Include(i => i.CourseAssignments)
                    .ThenInclude(i => i.Course)
                .FirstOrDefaultAsync(m => m.ID == id);

            if (await TryUpdateModelAsync<Instructor>(
                instructorToUpdate,
                "",
                i => i.FirstMidName, i => i.LastName, i => i.HireDate, i => i.OfficeAssignment))
            {
                if (string.IsNullOrWhiteSpace(instructorToUpdate.OfficeAssignment?.Location))
                {
                    instructorToUpdate.OfficeAssignment = null;
                }

                UpdateInstructorCourses(selectedCourses, instructorToUpdate);
                try
                {
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateException /* ex */)
                {
                    //Log the error (uncomment ex variable name and write a log.)
                    ModelState.AddModelError("", "Unable to save changes. " +
                        "Try again, and if the problem persists, " +
                        "see your system administrator.");
                }
                return RedirectToAction(nameof(Index));
            }
            UpdateInstructorCourses(selectedCourses, instructorToUpdate);
            PopulateAssignedCourseData(instructorToUpdate);
            return View(instructorToUpdate);
        }

        private void UpdateInstructorCourses(int[] selectedCourses, Instructor instructorToUpdate)
        {
            if (selectedCourses == null)
            {
                instructorToUpdate.CourseAssignments = new List<CourseAssignment>();
                return;
            }

            HashSet<int> selectedCoursesHS = new (selectedCourses);
            HashSet<int> instructorCourses = new (instructorToUpdate.CourseAssignments.Select(c => c.Course.CourseID));
            foreach (Course course in _context.Courses)
            {
                if (selectedCoursesHS.Contains(course.CourseID))
                {
                    if (!instructorCourses.Contains(course.CourseID))
                    {
                        instructorToUpdate.CourseAssignments.Add(new CourseAssignment { InstructorID = instructorToUpdate.ID, CourseID = course.CourseID });
                    }
                }
                else
                {

                    if (instructorCourses.Contains(course.CourseID))
                    {
                        CourseAssignment courseToRemove = instructorToUpdate.CourseAssignments.FirstOrDefault(i => i.CourseID == course.CourseID);
                        _context.Remove(courseToRemove);
                    }
                }
            }
        }

        // GET: Instructors/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            Instructor instructor = await _context.Instructors
                .FirstOrDefaultAsync(m => m.ID == id);
            return instructor == null ? NotFound() : (IActionResult)View(instructor);
        }

        // POST: Instructors/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            Instructor instructor = await _context.Instructors
                .Include(i => i.CourseAssignments)
                .SingleAsync(i => i.ID == id);

            List<Department> departments = await _context.Departments
                .Where(d => d.InstructorID == id)
                .ToListAsync();
            departments.ForEach(d => d.InstructorID = null);

            _context.Instructors.Remove(instructor);

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool InstructorExists(int id)
        {
            return _context.Instructors.Any(e => e.ID == id);
        }
    }
}
