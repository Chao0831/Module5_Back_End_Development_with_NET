// Data/SeedData.cs
using UserManagementAPI.Models;

namespace UserManagementAPI.Data;

public static class SeedData
{
    public static void Initialize(UserDbContext context)
    {
        try
        {
            // Check if there are any users
            if (context.Users.Any())
            {
                return; // Database has been seeded
            }

            // Add seed data with validation
            var users = new List<User>
            {
                new User
                {
                    FirstName = "John",
                    LastName = "Doe",
                    Email = "john.doe@techhive.com",
                    Department = "IT",
                    Role = "Developer",
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                },
                new User
                {
                    FirstName = "Jane",
                    LastName = "Smith",
                    Email = "jane.smith@techhive.com",
                    Department = "HR",
                    Role = "Manager",
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                },
                new User
                {
                    FirstName = "Bob",
                    LastName = "Johnson",
                    Email = "bob.johnson@techhive.com",
                    Department = "IT",
                    Role = "System Administrator",
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                }
            };

            context.Users.AddRange(users);
            context.SaveChanges();
        }
        catch (Exception ex)
        {
            // Log error but don't throw - seeding should not crash the app
            Console.WriteLine($"Error seeding database: {ex.Message}");
        }
    }
}