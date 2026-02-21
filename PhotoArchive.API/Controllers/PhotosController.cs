using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;
using PhotoArchive.API.DTOs;
using PhotoArchive.Domain.Entities;
using PhotoArchive.Infrastructure;

namespace PhotoArchive.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PhotosController(PhotoArchiveDbContext context) : ControllerBase
    {
        private static readonly FileExtensionContentTypeProvider ContentTypeProvider = new();
        private readonly PhotoArchiveDbContext _context = context;

        [HttpGet]
        public async Task<ActionResult<PagedResponse<PhotoDto>>> GetPhotos([FromQuery] PhotoQueryRequest request)
        {
            var query = _context.Photos
                .AsNoTracking()
                .AsQueryable();
            #region Filters

            if (!string.IsNullOrWhiteSpace(request.Extension))
            {
                var extension = request.Extension.StartsWith('.')
                    ? request.Extension
                    : $".{request.Extension}";
                query = query.Where(p => p.Extension == extension);
            }

            if (request.IsDuplicate.HasValue)
                query = query.Where(p => p.IsDuplicate == request.IsDuplicate.Value);

            if (request.GroupingYear.HasValue)
                query = query.Where(p => p.GroupingYear == request.GroupingYear.Value);

            if (request.GroupingDateFrom.HasValue)
                query = query.Where(p => p.GroupingDate >= request.GroupingDateFrom.Value);

            if (request.GroupingDateTo.HasValue)
                query = query.Where(p => p.GroupingDate <= request.GroupingDateTo.Value);

            if (request.TagIds.Length > 0)
                query = query.Where(p => p.PhotoTags.Any(pt => request.TagIds.Contains(pt.TagId)));

            if (request.PersonIds.Length > 0)
                query = query.Where(p => p.PhotoPeople.Any(pp => request.PersonIds.Contains(pp.PersonId)));
            #endregion
            query = query.OrderBy(p => p.GroupingDate);

            var totalCount = await query.CountAsync();

            var items = await query
                .Skip((request.PageNumber - 1) * request.PageSize)
                .Take(request.PageSize)
                .Select(p => new
                {
                    p.Id,
                    p.GroupingDate
                })
                .ToListAsync();

            var photoDtos = items.Select(p => new PhotoDto
            {
                Id = p.Id,
                GroupingDate = p.GroupingDate,
                ImageUrl = BuildImageUrl(p.Id)
            }).ToList();

            var response = new PagedResponse<PhotoDto>()
            {
                Items = photoDtos,
                PageNumber = request.PageNumber,
                PageSize = request.PageSize,
                TotalCount = totalCount,
                TotalPages = (int)Math.Ceiling(totalCount / (double)request.PageSize)
            };

            return Ok(response);
        }

        [HttpGet("{id:int}")]
        public async Task<ActionResult<PhotoDetailsDto>> GetById(int id)
        {
            var photo = await _context.Photos
                .AsNoTracking()
                .Where(p => p.Id == id)
                .Select(p => new
                {
                    p.Id,
                    p.GroupingDate,
                    People = p.PhotoPeople
                        .Select(pp => new PersonDto
                        {
                            Id = pp.PersonId,
                            Name = pp.Person != null ? pp.Person.Name : string.Empty
                        }),
                    Tags = p.PhotoTags
                        .Select(pt => new TagDto
                        {
                            Id = pt.TagId,
                            Name = pt.Tag != null ? pt.Tag.Name : string.Empty
                        })
                })
                .FirstOrDefaultAsync();

            if (photo == null)
                return NotFound("Could not find photo with id: " + id);

            var dto = new PhotoDetailsDto
            {
                Id = photo.Id,
                GroupingDate = photo.GroupingDate,
                ImageUrl = BuildImageUrl(photo.Id),
                People = [.. photo.People],
                Tags = [.. photo.Tags]
            };

            return Ok(dto);
        }

        [HttpGet("{id:int}/image")]
        public async Task<IActionResult> GetImage(int id)
        {
            var photo = await _context.Photos
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == id);
            if (photo == null || string.IsNullOrWhiteSpace(photo.OutputPath) || !System.IO.File.Exists(photo.OutputPath))
                return NotFound();

            if (!ContentTypeProvider.TryGetContentType(photo.OutputPath, out var contentType))
                contentType = "application/octet-stream";

            var stream = System.IO.File.OpenRead(photo.OutputPath);
            return File(stream, contentType, enableRangeProcessing: true);
        }

        [HttpPut("{id:int}")]
        public async Task<IActionResult> UpdatePhoto(int id, [FromBody] UpdatePhotoDto request)
        {
            var photo = await _context.Photos
                .Include(p => p.PhotoTags)
                .Include(p => p.PhotoPeople)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (photo == null)
                return NotFound("Could not find a photo with id: " + id);

            var requestedTagIds = (request.TagIds ?? []).Distinct().ToArray();
            var requestedPersonIds = (request.PersonIds ?? []).Distinct().ToArray();

            if (requestedTagIds.Length > 0)
            {
                var existingTagIds = await _context.Tags
                    .Where(t => requestedTagIds.Contains(t.Id))
                    .Select(t => t.Id)
                    .ToListAsync();
                var missingTagIds = requestedTagIds.Except(existingTagIds).ToArray();
                if (missingTagIds.Length > 0)
                    return BadRequest($"Could not find tag ids: {string.Join(", ", missingTagIds)}");
            }

            if (requestedPersonIds.Length > 0)
            {
                var existingPersonIds = await _context.People
                    .Where(p => requestedPersonIds.Contains(p.Id))
                    .Select(p => p.Id)
                    .ToListAsync();
                var missingPersonIds = requestedPersonIds.Except(existingPersonIds).ToArray();
                if (missingPersonIds.Length > 0)
                    return BadRequest($"Could not find person ids: {string.Join(", ", missingPersonIds)}");
            }

            foreach (int tagId in requestedTagIds)
            {
                if (!photo.PhotoTags.Any(p => p.TagId == tagId))
                    photo.PhotoTags.Add(new PhotoTag
                    {
                        PhotoId = photo.Id,
                        TagId = tagId
                    });
            }
            foreach (int personId in requestedPersonIds)
            {
                if (!photo.PhotoPeople.Any(p => p.PersonId == personId))
                    photo.PhotoPeople.Add(new PhotoPerson
                    {
                        PhotoId = photo.Id,
                        PersonId = personId
                    });
            }

            photo.GroupingDate = request.GroupingDate;

            _context.Photos.Update(photo);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        [HttpPost("{photoId:int}/people/{personId:int}")]
        public async Task<IActionResult> AddPersonToPhoto(int photoId, int personId)
        {
            if (!await _context.Photos.AnyAsync(p => p.Id == photoId))
                return NotFound($"Could not find a photo with id: {photoId}");

            if (!await _context.People.AnyAsync(p => p.Id == personId))
                return NotFound($"Could not find a person with id: {personId}");

            var alreadyLinked = await _context.PhotoPeople
                .AnyAsync(pp => pp.PhotoId == photoId && pp.PersonId == personId);
            if (alreadyLinked)
                return NoContent();

            _context.PhotoPeople.Add(new PhotoPerson
            {
                PhotoId = photoId,
                PersonId = personId
            });

            await _context.SaveChangesAsync();
            return NoContent();
        }

        [HttpDelete("{photoId:int}/people/{personId:int}")]
        public async Task<IActionResult> RemovePersonFromPhoto(int photoId, int personId)
        {
            if (!await _context.Photos.AnyAsync(p => p.Id == photoId))
                return NotFound($"Could not find a photo with id: {photoId}");

            var link = await _context.PhotoPeople
                .FirstOrDefaultAsync(pp => pp.PhotoId == photoId && pp.PersonId == personId);
            if (link == null)
                return NoContent();

            _context.PhotoPeople.Remove(link);
            await _context.SaveChangesAsync();
            return NoContent();
        }

        [HttpPost("{photoId:int}/tags/{tagId:int}")]
        public async Task<IActionResult> AddTagToPhoto(int photoId, int tagId)
        {
            if (!await _context.Photos.AnyAsync(p => p.Id == photoId))
                return NotFound($"Could not find a photo with id: {photoId}");

            if (!await _context.Tags.AnyAsync(t => t.Id == tagId))
                return NotFound($"Could not find a tag with id: {tagId}");

            var alreadyLinked = await _context.PhotoTags
                .AnyAsync(pt => pt.PhotoId == photoId && pt.TagId == tagId);
            if (alreadyLinked)
                return NoContent();

            _context.PhotoTags.Add(new PhotoTag
            {
                PhotoId = photoId,
                TagId = tagId
            });

            await _context.SaveChangesAsync();
            return NoContent();
        }

        [HttpDelete("{photoId:int}/tags/{tagId:int}")]
        public async Task<IActionResult> RemoveTagFromPhoto(int photoId, int tagId)
        {
            if (!await _context.Photos.AnyAsync(p => p.Id == photoId))
                return NotFound($"Could not find a photo with id: {photoId}");

            var link = await _context.PhotoTags
                .FirstOrDefaultAsync(pt => pt.PhotoId == photoId && pt.TagId == tagId);
            if (link == null)
                return NoContent();

            _context.PhotoTags.Remove(link);
            await _context.SaveChangesAsync();
            return NoContent();
        }

        private string BuildImageUrl(int photoId)
        {
            return Url.Action(nameof(GetImage), "Photos", new { id = photoId }, Request.Scheme)
                ?? $"/api/photos/{photoId}/image";
        }
    }
}


