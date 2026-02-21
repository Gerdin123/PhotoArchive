using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PhotoArchive.API.DTOs;
using PhotoArchive.Domain.Entities;
using PhotoArchive.Infrastructure;

// For more information on enabling Web API for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace PhotoArchive.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PeopleController(PhotoArchiveDbContext context) : ControllerBase
    {
        private readonly PhotoArchiveDbContext _context = context;

        // GET: api/<PeopleController>
        [HttpGet]
        public async Task<IEnumerable<PersonDto>> Get()
        {
            var people = await _context.People
                .OrderBy(p => p.Name)
                .ToListAsync();
            var dto = people.Select(p => new PersonDto
            {
                Id = p.Id,
                Name = p.Name,
            });
            return dto;
        }

        // GET api/<PeopleController>/5
        [HttpGet("{id:int}")]
        public async Task<ActionResult<PersonDto>> Get(int id)
        {
            var person = await _context.People
                .FirstOrDefaultAsync(p => p.Id == id);

            if (person == null)
                return NotFound("A person with that ID could not be found");

            var dto = new PersonDto { Id = person.Id, Name = person.Name };

            return Ok(dto);
        }

        // POST api/<PeopleController>
        [HttpPost]
        public async Task<ActionResult<PersonDto>> Post([FromBody] NameRequestDto request)
        {
            if (string.IsNullOrWhiteSpace(request.Name))
                return BadRequest("Name cannot be empty");

            var name = request.Name.Trim();
            var normalized = name.ToLowerInvariant();

            var nameExists = await _context.People.AnyAsync(p => p.NormalizedName == normalized);
            if (nameExists)
                return BadRequest("Name already exists, use that instead");

            Person newPerson = new()
            {
                Name = name,
                NormalizedName = normalized,
            };

            await _context.People.AddAsync(newPerson);
            await _context.SaveChangesAsync();
            
            var dto = new PersonDto
            {
                Id = newPerson.Id,
                Name = newPerson.Name
            };

            return CreatedAtAction(nameof(Get), new { id = newPerson.Id }, dto);
        }

        [HttpPut("{id:int}")]
        public async Task<ActionResult<PersonDto>> Put(int id, [FromBody] NameRequestDto request)
        {
            if (string.IsNullOrWhiteSpace(request.Name))
                return BadRequest("Name must have a value");

            var person = await _context.People.FirstOrDefaultAsync(p => p.Id == id);
            if (person == null)
                return NotFound($"Person with id {id} was not found");

            var name = request.Name.Trim();
            var normalized = name.ToLowerInvariant();
            var nameExists = await _context.People.AnyAsync(p => p.Id != id && p.NormalizedName == normalized);
            if (nameExists)
                return BadRequest("Name already exists, use that instead");

            person.Name = name;
            person.NormalizedName = normalized;

            _context.People.Update(person);
            await _context.SaveChangesAsync();

            return Ok(new PersonDto
            {
                Id = person.Id,
                Name = person.Name
            });
        }

        // DELETE api/<PeopleController>/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var person = await _context.People.FirstOrDefaultAsync(p => p.Id == id);
            if (person == null)
                return NotFound($"Person with Id: {id} was not found");

            _context.People.Remove(person);
            await _context.SaveChangesAsync();
            return NoContent();    
        }
    }
}
