# 🎬 Movie Booking API

An ASP.NET Core 9.0 RESTful API for managing movies, shows, theaters, bookings, and users.  
Built with **Entity Framework Core** and secured using **JWT Authentication**.  
Deployed on **Render** 🚀

## 🌐 Live API
[Movie Booking API](https://moviebookingapi.onrender.com/index.html)

Swagger UI is available for interactive API documentation.

---

## ✨ Features
- 🔐 **JWT Authentication** (Login & Register users)
- 🎥 **Movies Management** (Add, Update, Delete, List movies with images)
- 🏢 **Theaters & Screens** (Organize shows by theater/screen)
- ⏰ **Showtimes** (Link movies with theaters and timings)
- 💺 **Seat Booking System** (Reserve & lock seats with concurrency handling)
- 📖 **Swagger/OpenAPI** docs for easy testing

---

## ⚙️ Tech Stack
- **.NET 9.0**
- **ASP.NET Core Web API**
- **Entity Framework Core (Code First Migrations)**
- **SQL Server (Azure SQL / Local DB)**
- **JWT Bearer Authentication**
- Deployment: **Render (Docker)**

## 📦 Project Structure
```
MovieBookingAPI/
├── Controllers/ # API controllers
├── Models/ # Entity models
├── Data/ # DbContext & Seed data
├── Migrations/ # EF Core migrations
├── Program.cs # App entry point
├── Dockerfile # For Render deployment
└── render.yaml # Render service definition
```
---


## 🚀 Getting Started (Local Development)

### 1️⃣ Clone the repository
```bash
git clone https://github.com/Anish2944/movieBookingAPI.git
cd movieBookingAPI
```
---
### 2️⃣ Configure Environment Variables
Create a appsettings.Development.json (or use dotnet user-secrets) with:
```
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Database=MovieBookingDb;Trusted_Connection=True;TrustServerCertificate=True;"
  },
  "Jwt": {
    "Key": "your-secret-key-here",
    "Issuer": "moviebookingapi",
    "Audience": "moviebookingapi"
  }
}
```
### 3️⃣ Run Database Migrations

`dotnet ef database update`

### 4️⃣ Run the API

`dotnet run`

API will be available at:

`https://localhost:5001`

Swagger UI:

`https://localhost:[PORT]/swagger`

## 📚 API Endpoints
Auth

POST /api/auth/register → Register new user
POST /api/auth/login → Login & receive JWT

Movies
GET /api/movies → List movies
POST /api/movies → Add movie (Admin)
PUT /api/movies/{id} → Update movie
DELETE /api/movies/{id} → Delete movie

Bookings
POST /api/bookings → Create a booking
GET /api/bookings/my → Get logged-in user’s bookings
…and more (see Swagger for full list).

## 🐳 Deployment (Render)

This project uses Docker & render.yaml.
Render builds and runs the container automatically.

## 🤝 Contributing

Pull requests are welcome!
For major changes, please open an issue first to discuss what you’d like to change.

