FROM mcr.microsoft.com/mssql/server

ENV ACCEPT_EULA=Y
ENV SA_PASSWORD=p@ssw0rd
ENV MSSQL_PID=Developer
ENV MSSQL_TCP_PORT=1433

EXPOSE 1433

CMD /bin/bash ./entrypoint.sh