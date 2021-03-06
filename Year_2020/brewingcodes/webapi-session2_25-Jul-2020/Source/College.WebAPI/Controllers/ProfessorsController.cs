﻿using College.Core.Constants;
using College.Core.Entities;
using College.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;

namespace College.WebAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ProfessorsController : ControllerBase
    {

        private readonly ILogger<ProfessorsController> _logger;
        private readonly IProfessorsBLL _professorsBLL;
        private readonly IDistributedCache _cache;

        public ProfessorsController(ILogger<ProfessorsController> logger, IProfessorsBLL professorsBLL,
            IDistributedCache cache)
        {
            _logger = logger;

            _professorsBLL = professorsBLL;

            _cache = cache;
        }

        [HttpGet]
        [ProducesResponseType(typeof(IEnumerable<Professor>), (int)HttpStatusCode.OK)]
        public async Task<ActionResult<IEnumerable<Professor>>> Get()
        {
            IEnumerable<Professor> professors;

            _logger.Log(LogLevel.Debug, "Request Received for ProfessorsController::Get");

            // Verify the content exists in Redis cache
            var professorsFromCache = _cache.GetString(Constants.RedisCacheStore.AllProfessorsKey);

            if (!string.IsNullOrEmpty(professorsFromCache))
            {
                // content exists in Redis cache, deserilize
                professors = JsonConvert.DeserializeObject<IEnumerable<Professor>>(professorsFromCache);
            }
            else
            {
                // Retrieve it from SQL
                professors = await _professorsBLL.GetAllProfessors();

                // Store a copy in Redis Server
                _cache.SetString(Constants.RedisCacheStore.AllProfessorsKey, JsonConvert.SerializeObject(professors),
                        GetDistributedCacheEntryOptions());
            }

            _logger.Log(LogLevel.Debug, "Returning the results from ProfessorsController::Get");

            return Ok(professors);
        }

        [HttpGet("{id}", Name = nameof(GetProfessorById))]
        [ProducesResponseType(typeof(Professor), (int)HttpStatusCode.OK)]
        [ProducesResponseType((int)HttpStatusCode.NotFound)]
        public async Task<ActionResult<Professor>> GetProfessorById(Guid id)
        {
            Professor professor;
            string professorId = $"{Constants.RedisCacheStore.SingleProfessorsKey}{id}";

            _logger.Log(LogLevel.Debug, "Request Received for ProfessorsController::Get");

            var professorFromCache = _cache.GetString(professorId);
            if (!string.IsNullOrEmpty(professorFromCache))
            {
                //if they are there, deserialize them
                professor = JsonConvert.DeserializeObject<Professor>(professorFromCache);
            }
            else
            {
                // Going to Data Store SQL Server
                professor = await _professorsBLL.GetProfessorById(id);

                //and then, put them in cache
                _cache.SetString(professorId, JsonConvert.SerializeObject(professor), GetDistributedCacheEntryOptions());
            }

            if (professor == null)
            {
                return NotFound();
            }

            _logger.Log(LogLevel.Debug, "Returning the results from ProfessorsController::Get");

            return Ok(professor);
        }

        [HttpPost]
        [ProducesResponseType(typeof(Professor), (int)HttpStatusCode.Created)]
        public async Task<ActionResult<Professor>> AddProfessor([FromBody] Professor professor)
        {
            _logger.Log(LogLevel.Debug, "Request Received for ProfessorsController::AddProfessor");

            var createdProfessor = await _professorsBLL.AddProfessor(professor);

            _logger.Log(LogLevel.Debug, "Returning the results from ProfessorsController::AddProfessor");

            return CreatedAtRoute(routeName: nameof(GetProfessorById),
                                  routeValues: new { id = createdProfessor.ProfessorId },
                                  value: createdProfessor);
        }

        // PUT: HTTP 200 / HTTP 204 should imply "resource updated successfully". 
        [HttpPut]
        [ProducesResponseType((int)HttpStatusCode.NoContent)]
        public async Task<ActionResult> UpdateProfessor([FromBody] Professor professor)
        {
            var _ = await _professorsBLL.UpdateProfessor(professor);

            return NoContent();
        }

        // DELETE: HTTP 200 / HTTP 204 should imply "resource deleted successfully".
        [HttpDelete("{id}")]
        [ProducesResponseType((int)HttpStatusCode.NoContent)]
        [ProducesResponseType((int)HttpStatusCode.InternalServerError)]
        public async Task<ActionResult> DeleteProfessor(Guid id)
        {
            var professorDeleted = await _professorsBLL.DeleteProfessorById(id);

            if (!professorDeleted)
            {
                return StatusCode(500, $"Unable to delete Professor with id {id}");
            }

            return NoContent();
        }

        private DistributedCacheEntryOptions GetDistributedCacheEntryOptions()
        {
            return new DistributedCacheEntryOptions()
            {
                AbsoluteExpiration = new System.DateTimeOffset(DateTime.Now.ToUniversalTime().AddSeconds(60), new TimeSpan(0, 0, 0))
            };
        }

    }
}
