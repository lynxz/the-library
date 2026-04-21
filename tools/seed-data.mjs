import { TableClient } from "@azure/data-tables";
import { BlobServiceClient } from "@azure/storage-blob";
import bcrypt from "bcryptjs";

const connectionString = process.env.STORAGE_CONNECTION_STRING || "UseDevelopmentStorage=true";
const isDevelopmentStorage = connectionString === "UseDevelopmentStorage=true";

const adminPassword = process.env.SEED_ADMIN_PASSWORD || (isDevelopmentStorage ? "admin123" : "");
const readerPassword = process.env.SEED_READER_PASSWORD || (isDevelopmentStorage ? "reader123" : "");

if (!adminPassword || !readerPassword) {
  throw new Error("SEED_ADMIN_PASSWORD and SEED_READER_PASSWORD are required when seeding non-development storage.");
}

async function seed() {
  // --- Seed Users ---
  const usersTable = TableClient.fromConnectionString(connectionString, "Users");
  await usersTable.createTable().catch(() => {});

  const users = [
    { partitionKey: "USER", rowKey: "admin", password: adminPassword, isAdmin: true },
    { partitionKey: "USER", rowKey: "reader", password: readerPassword }
  ];

  for (const user of users) {
    const hash = bcrypt.hashSync(user.password, 10);
    await usersTable.upsertEntity(
      { partitionKey: user.partitionKey, rowKey: user.rowKey, PasswordHash: hash, IsAdmin: user.isAdmin ?? false },
      "Replace"
    );
    console.log(`  Upserted user: ${user.rowKey}`);
  }
  console.log("Users seeded.\n");

  // --- Seed Table Storage ---
  const tableClient = TableClient.fromConnectionString(connectionString, "BookMetadata");
  await tableClient.createTable().catch(() => {}); // ignore if exists

  const books = [
    {
      partitionKey: "BOOK",
      rowKey: "1",
      Title: "The Pragmatic Programmer",
      Author: "David Thomas & Andrew Hunt",
      Format: "PDF",
      BlobPath: "the-pragmatic-programmer.pdf",
      Description: "A classic guide to software craftsmanship covering topics from personal responsibility to architectural techniques."
    },
    {
      partitionKey: "BOOK",
      rowKey: "2",
      Title: "Clean Code",
      Author: "Robert C. Martin",
      Format: "EPUB",
      BlobPath: "clean-code.epub",
      Description: "A handbook of agile software craftsmanship with practical advice on writing readable, maintainable code."
    },
    {
      partitionKey: "BOOK",
      rowKey: "3",
      Title: "Designing Data-Intensive Applications",
      Author: "Martin Kleppmann",
      Format: "PDF",
      BlobPath: "designing-data-intensive-apps.pdf",
      Description: "An in-depth guide to the principles and practicalities of data systems and how to build reliable, scalable applications."
    }
  ];

  for (const book of books) {
    await tableClient.upsertEntity(book, "Replace");
    console.log(`  Upserted book: ${book.Title}`);
  }
  console.log("Table seeded.\n");

  // --- Seed Blob Storage ---
  const blobServiceClient = BlobServiceClient.fromConnectionString(connectionString);
  const containerClient = blobServiceClient.getContainerClient("books");
  await containerClient.createIfNotExists();

  for (const book of books) {
    const blobClient = containerClient.getBlockBlobClient(book.BlobPath);
    const content = `This is a placeholder ${book.Format} file for "${book.Title}" by ${book.Author}.`;
    await blobClient.upload(content, Buffer.byteLength(content), {
      blobHTTPHeaders: {
        blobContentType: book.Format === "PDF" ? "application/pdf" : "application/epub+zip"
      }
    });
    console.log(`  Uploaded blob: ${book.BlobPath}`);
  }
  console.log("Blobs seeded.\n");

  console.log(isDevelopmentStorage ? "Done! Test data is ready in Azurite." : "Done! Production seed data has been updated.");
}

seed().catch(console.error);
