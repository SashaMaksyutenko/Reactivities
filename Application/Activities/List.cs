using MediatR;
using Microsoft.EntityFrameworkCore;
using Persistence;
using Application.Core;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using Application.Interfaces;

namespace Application.Activities
{
    public class List
    {
        public class Query:IRequest<Result<List<ActivityDto>>>{}
        public class Handler : IRequestHandler<Query,Result<List<ActivityDto>>>
        {
        private readonly DataContext _context;
            private readonly IMapper _mapper;
            private readonly IUserAccessor _userAccessor;

            public Handler(DataContext context,IMapper mapper,IUserAccessor userAccessor){
                _context = context;
                _mapper = mapper;
                _userAccessor = userAccessor;
            }
            public async Task<Result<List<ActivityDto>>> Handle(Query request,CancellationToken token)
            {
                var activities=await _context.Activities
                .ProjectTo<ActivityDto>(_mapper.ConfigurationProvider,new{currentUsername=_userAccessor.GetUsername()})
                .ToListAsync(token);
                return Result<List<ActivityDto>>.Success(activities);
            }
        }
    }
}