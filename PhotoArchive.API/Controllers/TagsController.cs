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
    public class TagsController(PhotoArchiveDbContext context) : ControllerBase
    {
        private readonly PhotoArchiveDbContext _context = context;

        // GET: api/<TagsController>
        [HttpGet]
        public async Task<ActionResult<IEnumerable<TagDto>>> Get()
        {
            List<Tag> tags = await _context.Tags.ToListAsync() ?? [];
            List<TagDto> dtos = [.. tags.Select(p => new TagDto 
            { 
                Id = p.Id, 
                Name = p.Name 
            })];

            return Ok(dtos);
        }

        // GET api/<TagsController>/5
        [HttpGet("{id:int}")]
        public async Task<ActionResult<TagDto>> Get(int id)
        {
            Tag? tag = await _context.Tags.FirstOrDefaultAsync(p => p.Id == id);
            if (tag == null)
                return NotFound($"Tag with id: {id} was not found");

            return Ok(new TagDto
            {
                Id = tag.Id,
                Name = tag.Name
            });
        }

        // POST api/<TagsController>
        [HttpPost]
        public async Task<ActionResult<TagDto>> Post([FromBody] NameRequestDto request)
        {
            if (string.IsNullOrWhiteSpace(request.Name))
                return BadRequest("Name must have a value");

            var name = request.Name.Trim();
            var normalized = name.ToLowerInvariant();
            var nameExists = await _context.Tags.AnyAsync(p => p.NormalizedName == normalized);
            if (nameExists)
                return BadRequest("Name already exists, use that instead");

            var tag = new Tag { Name = name, NormalizedName = normalized };
            await _context.Tags.AddAsync(tag);
            await _context.SaveChangesAsync();

            var dto = new TagDto
            {
                Id = tag.Id,
                Name = tag.Name
            };

            return CreatedAtAction(nameof(Get), new { id = tag.Id }, dto);
        }

        // PUT api/<TagsController>/5
        [HttpPut("{id:int}")]
        public async Task<ActionResult<TagDto>> Put(int id, [FromBody] NameRequestDto request)
        {
            if (string.IsNullOrWhiteSpace(request.Name))
                return BadRequest("Name must have a value");

            var tag = await _context.Tags.FirstOrDefaultAsync(p => p.Id == id);
            if (tag == null)
                return NotFound($"Tag with id {id} was not found");

            var name = request.Name.Trim();
            var normalized = name.ToLowerInvariant();
            var nameExists = await _context.Tags.AnyAsync(p => p.Id != id && p.NormalizedName == normalized);
            if (nameExists)
                return BadRequest("Name already exists, use that instead");

            tag.Name = name;
            tag.NormalizedName = normalized;

            _context.Tags.Update(tag);
            await _context.SaveChangesAsync();

            return Ok(new TagDto
            {
                Id = tag.Id,
                Name = tag.Name
            });
        }

        // DELETE api/<TagsController>/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            Tag? tag = await _context.Tags.FirstOrDefaultAsync(t => t.Id == id);
            if(tag == null)
                return NotFound($"Tag with id {id} was not found");

            _context.Tags.Remove(tag);
            await _context.SaveChangesAsync();

            return NoContent();
        }
    }
}
