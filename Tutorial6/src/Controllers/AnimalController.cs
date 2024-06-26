using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Tutorial6.DTO;
using Tutorial6.Helpers;
using Tutorial6.Models;

namespace Tutorial6.Controllers
{
    [Route("api/animals")]
    [Authorize]
    [ApiController]
    public class AnimalController(AnimalClinicContext context) : ControllerBase
    {
        [HttpGet]
        public async Task<ActionResult<IEnumerable<GetAnimalDto>>> GetAnimals(string orderBy = "Name")
        {
            try
            {
                if (!IsValidOrderBy(orderBy.ToLower()))
                {
                    return BadRequest("Incorrect orderBy parameter. " +
                                      "Possible values include: name, description.");
                }

                var animals = await context.Animals.Include(a => a.AnimalType).ToListAsync();

                var sortedAnimals = orderBy.ToLower() switch
                {
                    "description" => animals.OrderBy(a => a.Description).ToList(),
                    _ => animals.OrderBy(a => a.Name).ToList()
                };

                return sortedAnimals.Select(MapToAnimalDto).ToList();
            }
            catch (Exception e)
            {
                return StatusCode(StatusCodes.Status500InternalServerError,
                    new { message = "Error occurred while getting animals.", details = e.Message });
            }
        }
        
        private static GetAnimalDto MapToAnimalDto(Animal animal)
        {
            return new GetAnimalDto
            {
                Id = animal.Id,
                Name = animal.Name,
                Description = animal.Description,
                AnimalType = animal.AnimalType.Name
            };
        }
        
        private static bool IsValidOrderBy(string orderBy)
        {
            return orderBy is "name" or "description";
        } 
        
        [HttpGet("{id:int}")]
        public async Task<ActionResult<GetAnimalDto>> GetAnimal(int id)
        {
            try
            {
                var animal = await context.Animals
                    .Include(a => a.AnimalType)
                    .FirstOrDefaultAsync(a => a.Id == id);

                return animal == null
                    ? NotFound($"Animal with Id {id} was not found.")
                    : MapToAnimalDtoWithoutId(animal);
            }
            catch (Exception e)
            {
                return StatusCode(StatusCodes.Status500InternalServerError,
                    new { message = "Error occurred while getting animal.", details = e.Message });
            }
        }
        
        private static GetAnimalDto MapToAnimalDtoWithoutId(Animal animal)
        {
            return new GetAnimalDto
            {
                Name = animal.Name,
                Description = animal.Description,
                AnimalType = animal.AnimalType.Name
            };
        }
        
        [HttpPost]
        public async Task<ActionResult<Animal>> AddAnimal(AddAnimalDto addAnimalDto)
        {
            try
            {
                var animalType = await context.AnimalTypes
                    .FirstOrDefaultAsync(at => at.Name.ToLower() == addAnimalDto.AnimalType.ToLower());

                if (animalType == null)
                {
                    return BadRequest("Animal with given animal type does not exist");
                }

                var animal = ConvertDtoToAnimal(addAnimalDto, animalType.Id);
                context.Animals.Add(animal);
                await context.SaveChangesAsync();

                return CreatedAtAction("GetAnimal", new { id = animal.Id }, MapToAnimalDto(animal));
            }
            catch (Exception e)
            {
                return StatusCode(StatusCodes.Status500InternalServerError,
                    new { message = "Error occurred while adding animal.", details = e.Message });
            }
        }

        private static Animal ConvertDtoToAnimal(AddAnimalDto addAnimalDto, int animalTypeId)
        {
            return new Animal
            {
                Name = addAnimalDto.Name,
                Description = string.IsNullOrEmpty(addAnimalDto.Description) ? null : addAnimalDto.Description,
                AnimalTypesId = animalTypeId
            };
        }

        [HttpPut("{id:int}")]
        public async Task<ActionResult<Animal>> UpdateAnimal(int id, UpdateAnimalDto updateAnimalDto)
        {
            try
            {
                var animal = await context.Animals.FindAsync(id);

                if (animal == null)
                {
                    return NotFound($"Animal with Id {id} was not found.");
                }

                animal.Name = updateAnimalDto.Name;
                animal.Description = string.IsNullOrEmpty(updateAnimalDto.Description)
                    ? null
                    : updateAnimalDto.Description;

                context.Entry(animal).State = EntityState.Modified;

                try
                {
                    await context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    return Conflict("Animal with given Id is already being modified by another user");
                }

                return NoContent();
            }
            catch (Exception e)
            {
                return StatusCode(StatusCodes.Status500InternalServerError,
                    new { message = "Error occurred while updating animal.", details = e.Message });
            }
        }
        
        
        [HttpDelete("{id:int}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteAnimal(int id)
        {
            try
            {
                var animal = await context.Animals.FindAsync(id);

                if (animal == null)
                {
                    return NotFound($"Animal with Id {id} was not found.");
                }

                context.Animals.Remove(animal);
                await context.SaveChangesAsync();

                return NoContent();
            }
            catch (Exception e)
            {
                return StatusCode(StatusCodes.Status500InternalServerError,
                    new { message = "Error occurred while deleting animal.", details = e.Message });
            }
        }
    }
}
